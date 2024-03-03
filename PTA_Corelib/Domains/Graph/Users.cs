using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using KTI.PAL.Teams.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Domains.Graph
{
    public interface IGraphUserDomain : IGraphDomain<Microsoft.Graph.User>
    {
        /// <summary>
        /// Get multiple users from either their Azure Active Directory object ID or email address.
        /// </summary>
        /// <param name="emailsOrIDs">A list of AAD object IDs or email addresses.</param>
        /// <returns>An awaitable Task containing a list of users.</returns>
        Task<List<Microsoft.Graph.User>> GetMultiple(IEnumerable<string> emailsOrIDs);
        /// <summary>
        /// Deactivate a user account.
        /// </summary>
        /// <param name="id">The Azure Active Directory object ID of the user account to deactivate.</param>
        /// <returns>An awaitable Task containing a boolean whether or not the deactivation was successful.</returns>
        Task<bool> Deactivate(string id);
    }

    public class Users : IGraphUserDomain
    {
        private readonly ILogger<Users> _logger;
        private readonly IGraphService _service;
        public Users() { }

        public Users(ILogger<Users> logger, IGraphService service)
        {
            _logger = logger;
            _service = service;
        }

        public Task<string> Create(User user)
        {
            throw new System.NotSupportedException();
        }

        public async Task<User> Get(string id)
        {
            User user;
            try
            {
                user = await _service.GetClient().Users[id].Request().GetAsync();
                return user;
            }
            catch (Microsoft.Graph.ServiceException e)
            {
                if (e.StatusCode.Equals(System.Net.HttpStatusCode.NotFound))
                {
                    _logger.LogWarning($"User with identifier {id} does not exist.");
                    return null;
                }
                else
                {
                    _logger.LogError(e, e.Message);
                }
            }
            return null;
        }

        public async Task<User> Update(User user)
        {
            return await _service.GetClient().Users[user.Id].Request().UpdateAsync(user);
        }

        public Task<bool> Delete(string id)
        {
            throw new System.NotSupportedException();
        }

        public async Task<List<User>> GetMultiple(IEnumerable<string> emailsOrIDs)
        {
            List<User> users = new();

            var batches = emailsOrIDs.Chunk(20).ToList();
            _logger.LogInformation($"Splitting request into {batches.Count} batches.");

            foreach (var batch in batches)
            {
                BatchRequestContent request = new();
                List<string> ids = new();
                foreach (string id in batch)
                    ids.Add(request.AddBatchRequestStep(_service.GetClient().Users[id].Request()));

                BatchResponseContent response = await _service.GetClient().Batch.Request().PostAsync(request);

                uint b = 0;
                foreach (string id in ids)
                {
                    b++;
                    try
                    {
                        HttpResponseMessage r = await response.GetResponseByIdAsync(id);
                        if (!r.StatusCode.Equals(HttpStatusCode.NotFound))
                            users.Add(await response.GetResponseByIdAsync<User>(id));
                    }
                    catch (Microsoft.Graph.ServiceException e)
                    {
                        if (e.StatusCode.Equals(HttpStatusCode.NotFound))
                            continue;
                        else
                            _logger.LogError(e, $"Exception in batch {b}: {e.Message}");
                    }
                }
            }

            return users;
        }

        public async Task<bool> Deactivate(string id)
        {
            Microsoft.Graph.User user = await this.Get(id);

            if (user is not null)
            {
                if (user.AccountEnabled.HasValue && !user.AccountEnabled.Value)
                    return false;

                Microsoft.Graph.User accountDisabled = new()
                {
                    AccountEnabled = false
                };

                await _service.GetClient().Users[id].Request().UpdateAsync(accountDisabled);
                return true;
            }

            return false;
        }
    }
}
