using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using KTI.PAL.Teams.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Domains.Graph
{
    public interface IGraphTeamDomain : IGraphDomain<Team>
    {
        /// <summary>
        /// Create a team.
        /// </summary>
        /// <param name="name">The team name.</param>
        /// <param name="description">The team description.</param>
        /// <param name="owner">The ID of the owner of the team. If not provided, will fallback to the stored Owner ID in App Configuration.</param>
        /// <returns>The ID of the newly created Team.</returns>
        Task<string> Create(string name, string description, string owner = null);
        /// <summary>
        /// Archive a single team.
        /// </summary>
        /// <param name="id">The ID of the team to archive.</param>
        /// <returns>An awaitable Task containing a boolean whether or not the team was successfully archived.</returns>
        Task<bool> Archive(string id);
        /// <summary>
        /// Archive multiple teams.
        /// </summary>
        /// <param name="ids">A list of team IDs to archive.</param>
        /// <returns>An awaitable Task.</returns>
        Task ArchiveMultiple(IEnumerable<string> ids);
        /// <summary>
        /// Add a single member to a team.
        /// </summary>
        /// <param name="teamId">The ID of the team to add to.</param>
        /// <param name="userId">The ID of the user to add.</param>
        /// <param name="roles">The roles of the user. Defaults to "member."</param>
        /// <returns>An awaitable Task containing a boolean whether or not the user was successfully added.</returns>
        Task<bool> AddMember(string teamId, string userId, IEnumerable<string> roles = null);
        /// <summary>
        /// Add multiple members to a team.
        /// </summary>
        /// <param name="teamId">The team ID to add to.</param>
        /// <param name="users">A list of user IDs to add.</param>
        /// <returns>An awaitable Task.</returns>
        Task AddMembers(string teamId, IEnumerable<string> users);
        /// <summary>
        /// Add multiple members to multiple teams.
        /// </summary>
        /// <param name="teams">A Dictionary where the key is the team ID, and its value is a list of the user IDs to add.</param>
        /// <returns>An awaitable Task.</returns>
        Task AddMembersMultiple(Dictionary<string, IEnumerable<string>> teams);
        /// <summary>
        /// Get Team information by looking up the Team name.
        /// </summary>
        /// <param name="displayName">The full or partial name of the Team to retrieve.</param>
        /// <returns>An awaitable Task containing a Team object.</returns>
        Task<Team> SearchByName(string displayName);
    }

    public class Teams : IGraphTeamDomain
    {
        private readonly ILogger<Teams> _logger;
        private readonly IGraphService _service;
        private readonly Config _config;
        public Teams() { }

        public Teams(ILogger<Teams> logger, IGraphService service, Config config)
        {
            _logger = logger;
            _service = service;
            _config = config;
        }

        public async Task<string> Create(Team team)
        {
            var response = await _service.GetClient().Teams.Request().AddResponseAsync(team);
            if (response.HttpHeaders.TryGetValues("Location", out var headers))
                return headers?.First().Split('\'', StringSplitOptions.RemoveEmptyEntries)[1];
            return null;
        }

        public async Task<string> Create(string name, string description, string owner = null)
        {
            if (string.IsNullOrWhiteSpace(owner))
                owner = _config.GetValue("Teams:Owner");
            Microsoft.Graph.Team team = new()
            {
                DisplayName = name,
                Description = description,
                Members = new TeamMembersCollectionPage()
                {
                    new AadUserConversationMember
                    {
                        Roles = new string[] {"owner"},
                        AdditionalData = new Dictionary<string, object>()
                            { {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{owner}')"} }
                    }
                },
                Channels = new TeamChannelsCollectionPage {
                    new Channel{
                        DisplayName = "Pilots",
                        Description = ""
                    },
                    new Channel{
                        DisplayName = "Cabin Crew",
                        Description = ""
                    },
                    new Channel{
                        DisplayName = "Flight Services",
                        Description = ""
                    },
                    new Channel{
                        DisplayName = "Flight Operations",
                        Description = ""
                    }
                },
                AdditionalData = new Dictionary<string, object>()
                { {"template@odata.bind", "https://graph.microsoft.com/v1.0/teamsTemplates('standard')"} }
            };

            var additionalMembers = _config.GetValue("Teams:AdditionalMembers").Split('\n');
            foreach (var id in additionalMembers)
                team.Members.Add(new AadUserConversationMember
                {
                    Roles = new string[] { "member" },
                    AdditionalData = new Dictionary<string, object>()
                            { {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{id}')"} }
                });

            return await Create(team);
        }

        public async Task<Team> Get(string id)
        {
            try
            {
                return await _service.GetClient().Teams[id].Request().GetAsync();
            }
            catch (Microsoft.Graph.ServiceException)
            {
                return null;
            }
        }

        public async Task<List<Team>> GetMultiple(IEnumerable<string> ids)
        {
            List<Team> teams = new();
            var batches = ids.Chunk(20);

            foreach (var batch in batches)
            {
                BatchRequestContent request = new();
                List<string> requestIds = new();
                foreach (string id in batch)
                    requestIds.Add(request.AddBatchRequestStep(_service.GetClient().Teams[id].Request()));

                BatchResponseContent batchResponse = await _service.GetClient().Batch.Request().PostAsync(request);

                foreach (string id in requestIds)
                {
                    teams.Add(await batchResponse.GetResponseByIdAsync<Team>(id));
                }
            }

            return teams;

        }

        public Task<Team> Update(Team team)
        {
            return _service.GetClient().Teams[team.Id].Request().UpdateAsync(team);
        }

        public async Task<bool> Delete(string id)
        {
            if (await this.Get(id) is not null)
            {
                await _service.GetClient().Teams[id].Request().DeleteAsync();
                return true;
            }

            return false;
        }

        public async Task<bool> Archive(string id)
        {
            Team team = await this.Get(id);

            if (team is not null)
            {
                if (team.IsArchived ?? false)
                {
                    return false;
                }

                Team archivedTeam = new()
                {
                    DisplayName = $"(archived) {team.DisplayName}",
                    Description = $"{team.Description} -- Archived on {DateTime.UtcNow}",
                };

                await _service.GetClient().Teams[team.Id].Request().UpdateAsync(archivedTeam);
                await _service.GetClient().Teams[team.Id].Archive().Request().PostAsync();
                return true;
            }

            return false;
        }

        public async Task ArchiveMultiple(IEnumerable<string> ids)
        {
            List<Team> teams = await this.GetMultiple(ids);
            List<Team> unarchived = new();

            unarchived.AddRange(teams.Where(team => team.IsArchived.HasValue && !team.IsArchived.Value));

            var batches = unarchived.Chunk(20);

            foreach (var batch in batches)
            {
                BatchRequestContent request = new();
                List<string> requestIds = new();
                var serializer = new Microsoft.Graph.Serializer();
                foreach (Team team in batch)
                {
                    var req = _service.GetClient().Teams[team.Id].Archive().Request().GetHttpRequestMessage();
                    req.Method = HttpMethod.Post;
                    req.Content = new StringContent(JsonConvert.SerializeObject(new { shouldSetSpoSiteReadOnlyForMembers = false }), Encoding.UTF8, "application/json");
                    requestIds.Add(request.AddBatchRequestStep(req));
                }

                BatchResponseContent response = await _service.GetClient().Batch.Request().PostAsync(request);
            }
        }

        public async Task<bool> AddMember(string teamId, string userId, IEnumerable<string> roles = null)
        {
            ITeamMembersCollectionPage members = await _service.GetClient().Teams[teamId].Members.Request().GetAsync();

            if (members.Select(m => m.Id).Contains(userId))
                return false;


            Team team = await this.Get(teamId);

            var newMember = new AadUserConversationMember()
            {
                Id = userId,
                Roles = roles ?? new List<string>() { "member" },
                // ODataType = null,
                AdditionalData = new Dictionary<string, object>()
                {
                    {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{userId}')"}
                }
            };

            var response = await _service.GetClient().Teams[teamId].Members.Request().AddAsync(newMember);
            if (response != null)
                return true;

            return false;
        }

        public async Task AddMembers(string teamId, IEnumerable<string> users)
        {
            await _service.GetClient().Teams[teamId].Members.Add(users.Select(user => new AadUserConversationMember
            {
                Roles = new string[] { "member" },
                AdditionalData = new Dictionary<string, object>() {
                    {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{user}')"}
                }
            })).Request().PostAsync();
        }

        public async Task AddMembersMultiple(Dictionary<string, IEnumerable<string>> teams)
        {
            var batches = teams.Chunk(20);

            foreach (var batch in batches)
            {
                BatchRequestContent request = new();
                List<string> requestIds = new();
                foreach (var team in batch)
                {
                    var users = team.Value.Select(user => new Dictionary<string, dynamic>{
                        {"roles", new string[] {"member"}},
                        {"@odata.type", "microsoft.graph.aadUserConversationMember"},
                        {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{user}')"}
                    });
                    var req = _service.GetClient().Teams[team.Key].Members.Add().Request().GetHttpRequestMessage();
                    req.Method = HttpMethod.Post;
                    req.Content = new StringContent(JsonConvert.SerializeObject(new { values = users }), Encoding.UTF8, "application/json");

                    requestIds.Add(request.AddBatchRequestStep(req));
                }

                BatchResponseContent batchResponse = await _service.GetClient().Batch.Request().PostAsync(request);
                foreach (string id in requestIds)
                {
                    var response = await batchResponse.GetResponseByIdAsync(id);
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        public async Task<Team> SearchByName(string displayName)
        {
            var group = (await _service.GetClient().Groups.Request().Filter($"startsWith(displayName,'{displayName}')").GetAsync()).FirstOrDefault();

            if (group is null)
                return null;

            return await _service.GetClient().Teams[group.Id].Request().GetAsync();
        }
    }
}
