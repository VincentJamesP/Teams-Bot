using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

using KTI.PAL.Teams.Core.Services;
using KTI.PAL.Teams.Core.Models.Dataverse;
using KTI.PAL.Teams.Core.Models;

namespace KTI.PAL.Teams.Core.Domains.Dataverse
{
    public interface IDataverseCrew
    {
        /// <summary>
        /// Get a single crew member from the Dataverse table.
        /// </summary>
        /// <param name="id">The merlot employee code to retrieve.</param>
        /// <returns>The crew member whose employee code matches the provided one; null if entry doesn't exist.</returns>
        Models.Dataverse.Crew Get(Guid id);
        /// <summary>
        /// Get a single crew member from the Dataverse table. At least one of the parameters must be provided.
        /// </summary>
        /// <param name="id">The ID of the entry stored in Dataverse.</param>
        /// <param name="employeeId">The Merlot employee code of the crew entry to retrieve.</param>
        /// <param name="AadUserId">The Azure Active Directory object ID of the crew.</param>
        /// <param name="email">The email address of the user.</param>
        /// <returns>The crew member whose entry matches any of the provided parameters; null if no matches are found.</returns>
        Models.Dataverse.Crew Get(Guid? id = null, string employeeId = null, string AadUserId = null, string email = null, string name = null);
        /// <summary>
        /// Get multiple crew from the Dataverse table.
        /// </summary>
        /// <param name="ids">A list of employee codes to retrieve.</param>
        /// <returns>A list of crew whose employee code matches one of the provided codes.</returns>
        List<Models.Dataverse.Crew> GetMultiple(IEnumerable<string> ids);

        /// <summary>
        /// Get multiple crew from the Dataverse table.
        /// </summary>
        /// <param name="ids">A list of Azure Active Directory object ids to retrieve.</param>
        /// <returns>A list of crew whose user id matches one of the provided ids.</returns>
        List<Models.Dataverse.Crew> GetMultipleByAADId(IEnumerable<string> ids);

        /// <summary>
        /// Add an entry in the Dataverse table.
        /// </summary>
        /// <param name="crew">The crew member to create, already cast into a Dataverse.Crew object.</param>
        /// <returns>The Guid of the created entry.</returns>
        Guid Create(Models.Dataverse.Crew crew);
        /// <summary>
        /// Add multiple entries in the Dataverse table.
        /// </summary>
        /// <param name="crew">A list of crew members to be added.</param>
        /// <returns>The Guids of the created entries.</returns>
        List<Guid> CreateMultiple(IEnumerable<Models.Dataverse.Crew> crew);

        /// <summary>
        /// Update a crew member's entry in the Dataverse table.
        /// </summary>
        /// <param name="crew">The crew member to update. Crew.Id must not be null.</param>
        void Update(Models.Dataverse.Crew crew);
        /// <summary>
        /// Update multiple crew members' entries in the Dataverse table.
        /// </summary>
        /// <param name="crew">A list of crew members to be updated.</param>
        void UpdateMultiple(IEnumerable<Models.Dataverse.Crew> crew);

        /// <summary>
        /// Remove a crew member entry from the Dataverse table.
        /// </summary>
        /// <param name="id">The Guid of the entry to remove.</param>
        void Delete(Guid id);
        /// <summary>
        /// Remove multiple crew members' entries from the Dataverse table.
        /// </summary>
        /// <param name="ids">A list of Guids to remove.</param>
        void DeleteMultiple(IEnumerable<Guid> ids);

        /// <summary>
        /// Either create or update a crew member's entry in the Dataverse table.
        /// Makes an additional IDataverseCrew.Get call to check the entry exists.
        /// </summary>
        /// <param name="crew">The crew member to upsert.</param>
        /// <returns>A Guid if an entry was created, null otherwise.</returns>
        Guid? Upsert(Models.Dataverse.Crew crew);
        /// <summary>
        /// Create or Update multiple crew members' entries in the Dataverse table.
        /// Makes an additional IDataverseCrew.GetMultiple call to check if the entries exist.
        /// </summary>
        /// <param name="crew">A list of crew members to upsert.</param>
        /// <returns>A list of Guids of created entries. Returns an empty list if no entries need to be created.</returns>
        List<Guid> UpsertMultiple(IEnumerable<Models.Dataverse.Crew> crew);

        /// <summary>
        /// Search for crew members by name and optionally filter by rank.
        /// </summary>
        /// <param name="name">A string that may be a complete name, or part of a name.</param>
        /// <param name="rank">If this is not null, only return crew members that are of the same rank.</param>
        /// <returns>A list of crew whose name matches or is similar to the provided value.</returns>
        List<Models.Dataverse.Crew> SearchByName(string name, string rank = null);
    }

    public class Crew : IDataverseCrew
    {
        private readonly ILogger<Crew> _logger;
        private readonly IDataverseService _service;
        private readonly string prefix;
        private readonly string tableName;
        private readonly string[] columns;

        public Crew() { }

        public Crew(ILogger<Crew> logger, IDataverseService service, Config config)
        {
            _logger = logger;
            _service = service;
            prefix = config.GetValue("Dataverse:Prefix");
            tableName = config.GetValue("Dataverse:CrewTable");
            columns = new string[] { $"{prefix}aaduserid", $"{prefix}email", $"{prefix}employeeid", $"{prefix}name", $"{prefix}rank", $"{prefix}{tableName}id" };
        }

        public Models.Dataverse.Crew Get(Guid id)
        {
            return Get(id: id);
        }

        public Models.Dataverse.Crew Get(Guid? id = null, string employeeId = null, string AadUserId = null, string email = null, string name = null)
        {
            if (!id.HasValue && string.IsNullOrWhiteSpace(employeeId) && string.IsNullOrWhiteSpace(AadUserId) && string.IsNullOrWhiteSpace(email))
                throw new ArgumentNullException("Please provide at least one parameter.");

            List<ConditionExpression> conditions = new();
            if (id.HasValue)
            {
                conditions.Add(new ConditionExpression($"{prefix}{tableName}id", ConditionOperator.Equal, id.Value));
            }
            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                conditions.Add(new ConditionExpression($"{prefix}employeeid", ConditionOperator.Equal, employeeId));
            }
            if (!string.IsNullOrWhiteSpace(AadUserId))
            {
                conditions.Add(new ConditionExpression($"{prefix}aaduserid", ConditionOperator.Equal, AadUserId));
            }
            if (!string.IsNullOrWhiteSpace(email))
            {
                conditions.Add(new ConditionExpression($"{prefix}email", ConditionOperator.Equal, email));
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                conditions.Add(new ConditionExpression($"{prefix}name", ConditionOperator.Like, $"%{name}%"));
            }

            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            conditions.ForEach(c => query.Criteria.AddCondition(c));

            // need to use RetrieveMultiple since that's the only Retrieve method that allows search criteria
            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return null;

            return new Models.Dataverse.Crew(response.Entities.First(), prefix, tableName);
        }

        public List<Models.Dataverse.Crew> GetMultiple(IEnumerable<string> ids)
        {
            var batches = ids.Chunk(1000);
            uint b = 0;
            List<Models.Dataverse.Crew> results = new();

            foreach (var batch in batches)
            {
                b++;
                ConditionExpression condition = new($"{prefix}employeeid", ConditionOperator.In, batch);

                QueryExpression query = new($"{prefix}{tableName}");
                query.ColumnSet.AddColumns(columns);
                query.Criteria.AddCondition(condition);

                var response = _service.GetClient().RetrieveMultiple(query);

                if (response.Entities.Count > 0)
                    results.AddRange(response.Entities.Select(e => new Models.Dataverse.Crew(e, prefix, tableName)));
            }

            return results;
        }

        public List<Models.Dataverse.Crew> GetMultipleByAADId(IEnumerable<string> ids)
        {
            var batches = ids.Chunk(1000);
            List<Models.Dataverse.Crew> results = new();

            foreach (var batch in batches)
            {
                ConditionExpression condition = new($"{prefix}aaduserid", ConditionOperator.In, batch);

                QueryExpression query = new($"{prefix}{tableName}");
                query.ColumnSet.AddColumns(columns);
                query.Criteria.AddCondition(condition);

                var response = _service.GetClient().RetrieveMultiple(query);

                if (response.Entities.Count > 0)
                    results.AddRange(response.Entities.Select(e => new Models.Dataverse.Crew(e, prefix, tableName)));
            }

            return results;
        }

        public Guid Create(Models.Dataverse.Crew crew)
        {
            Microsoft.Xrm.Sdk.Entity entity = crew.ToEntity(tableName, prefix);

            return _service.GetClient().Create(entity);
        }

        public List<Guid> CreateMultiple(IEnumerable<Models.Dataverse.Crew> crew)
        {
            var batches = crew.Chunk(1000);
            List<Guid> results = new();

            foreach (var batch in batches)
            {
                List<Microsoft.Xrm.Sdk.Entity> entities = batch.Select(crew => crew.ToEntity(tableName, prefix)).ToList();

                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = false,
                        ReturnResponses = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(entities.Select(e =>
                    new CreateRequest() { Target = e }
                ));

                ExecuteMultipleResponse responses = (ExecuteMultipleResponse)_service.GetClient().Execute(requests);

                if (responses.IsFaulted)
                {
                    throw new DataverseException(responses.Responses.First().Fault.Message);
                }

                results.AddRange(responses.Responses.Select(r => ((CreateResponse)r.Response).id).ToList());
            }

            return results;
        }

        public void Delete(Guid id)
        {
            _service.GetClient().Delete($"{prefix}{tableName}", id);
        }

        public void DeleteMultiple(IEnumerable<Guid> ids)
        {
            var batches = ids.Chunk(1000);

            foreach (var batch in batches)
            {
                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(batch.Select(id => new DeleteRequest() { Target = new EntityReference($"{prefix}{tableName}", id) }));

                _service.GetClient().Execute(requests);
            }
        }

        public void Update(Models.Dataverse.Crew crew)
        {
            Microsoft.Xrm.Sdk.Entity entity = crew.ToUpdateEntity(tableName, prefix);

            _service.GetClient().Update(entity);
        }

        public void UpdateMultiple(IEnumerable<Models.Dataverse.Crew> crew)
        {
            var batches = crew.Chunk(1000);
            uint b = 0;

            foreach (var batch in batches)
            {
                List<Microsoft.Xrm.Sdk.Entity> entities = batch.Select(c => c.ToUpdateEntity(tableName, prefix)).ToList();

                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings
                    { ContinueOnError = false },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(entities.Select(e => new UpdateRequest() { Target = e }));
                _service.GetClient().Execute(requests);

                _logger.LogInformation($"Batch {b}: Updated {entities.Count} entities.");
            }
        }

        public Guid? Upsert(Models.Dataverse.Crew crew)
        {
            var entity = Get(employeeId: crew.EmployeeId);
            if (entity is null)
                return Create(crew);
            else
                Update(crew);
            return null;
        }

        public List<Guid> UpsertMultiple(IEnumerable<Models.Dataverse.Crew> crew)
        {
            List<Guid> responses = new();

            var entities = GetMultiple(crew.Select(d => d.AadUserId));

            var toCreate = crew.Where(d => entities.All(e => e.AadUserId != d.AadUserId && !d.Id.HasValue)).ToList();
            var toUpdate = entities.Select(e =>
            {
                var c = crew.Where(d => d.AadUserId.Equals(e.AadUserId) || d.Id.Equals(e.Id)).FirstOrDefault();
                if (c is not null)
                {
                    c.Id = e.Id;
                    return c;
                }
                return null;
            }).ToList();
            toUpdate.RemoveAll(d => d is null);

            if (toUpdate.Count == 0 && toCreate.Count == 0)
            {
                _logger.LogInformation($"No operations to perform.");
                return new List<Guid>();
            }

            if (toUpdate.Count > 0)
                UpdateMultiple(toUpdate);
            if (toCreate.Count > 0)
                responses.AddRange(CreateMultiple(toCreate));

            if (responses.Count > 0)
                return responses;

            return new List<Guid>();
        }

        public List<Models.Dataverse.Crew> SearchByName(string name, string rank)
        {
            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            query.Criteria.AddCondition(new ConditionExpression($"{prefix}name", ConditionOperator.Like, $"%{name}%"));
            query.TopCount = 100;

            if (!string.IsNullOrWhiteSpace(rank))
                query.Criteria.AddCondition(new ConditionExpression($"{prefix}rank", ConditionOperator.Equal, rank));

            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return new List<Models.Dataverse.Crew>();

            return response.Entities.Select(e => new Models.Dataverse.Crew(e, prefix, tableName)).ToList();
        }
    }
}
