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
    public interface IDataverseDuty
    {
        /// <summary>
        /// Get a single flight duty from the Dataverse table.
        /// </summary>
        /// <param name="id">The ID in Merlot of the flight duty to retrieve.</param>
        /// <returns>The flight duty record whose Merlot ID matches the provided one; null if the entry doesn't exist.</returns>
        Models.Dataverse.Duty Get(string id);
        /// <summary>
        /// Get multiple flight duties from the Dataverse table.
        /// </summary>
        /// <param name="ids">A list of Merlot IDs of flights to retrieve.</param>
        /// <returns>A list of duties whose ID matches one of the provided IDs.</returns>
        List<Models.Dataverse.Duty> GetMultiple(IEnumerable<string> ids);

        /// <summary>
        /// Add an entry in the Dataverse table.
        /// </summary>
        /// <param name="duty">The duty to create, already cast into a Dataverse.Duty object.</param>
        /// <returns>The Guid of the created entry.</returns>
        Guid Create(Models.Dataverse.Duty duty);
        /// <summary>
        /// Add multiple entries in the Dataverse table.
        /// </summary>
        /// <param name="duties">A list of flight duties to be added.</param>
        /// <returns>The Guids of the created entries.</returns>
        List<Guid> CreateMultiple(IEnumerable<Models.Dataverse.Duty> duties);

        /// <summary>
        /// Update a flight duty entry in the Dataverse table.
        /// </summary>
        /// <param name="duty">The duty to update. Duty.Id must not be null.</param>
        void Update(Models.Dataverse.Duty duty);
        /// <summary>
        /// Update multiple entries in the Dataverse table.
        /// </summary>
        /// <param name="duties">The list of flight duties to be updated.</param>
        void UpdateMultiple(IEnumerable<Models.Dataverse.Duty> duties);

        /// <summary>
        /// Remove an entry from the Dataverse table.
        /// </summary>
        /// <param name="id">The Guid of the entry to remove.</param>
        void Delete(Guid id);
        /// <summary>
        /// Remove multiple entries from the Dataverse table.
        /// </summary>
        /// <param name="ids">A list of Guids of entries to remove.</param>
        void DeleteMultiple(IEnumerable<Guid> ids);

        /// <summary>
        /// Either create or update a flight duty's entry in the Dataverse table.
        /// Makes an additional IDataverseDuty.Get call to check the entry exists.
        /// </summary>
        /// <param name="duty">The flight duty to upsert.</param>
        /// <returns>A Guid if an entry was created, null otherwise.</returns>
        Guid? Upsert(Models.Dataverse.Duty duty);
        /// <summary>
        /// Create or Update multiple flight duties' entries in the Dataverse table.
        /// Makes an additional IDataverseDuty.GetMultiple call to check if the entries exist.
        /// </summary>
        /// <param name="duties">A list of flight duties to upsert.</param>
        /// <returns>A list of Guids of created entries. Returns an empty list if no entries were created.</returns>
        List<Guid> UpsertMultiple(IEnumerable<Models.Dataverse.Duty> duties);

        /// <summary>
        /// Get a list of flight duties containing a specific crew member.
        /// </summary>
        /// <param name="id">The Merlot empCode of the crew member to search for.</param>
        /// <param name="search">An optional string that the flight duty label should match, in whole or partially.</param>
        /// <returns>A list of flight duties that contain the specified id or search term.</returns>
        List<Models.Dataverse.Duty> GetByCrew(string id, string search = null);
        /// <summary>
        /// Get a list of flight duties from the past.
        /// </summary>
        /// <returns>A list of flight duties where Duty.End is over 48 hours ago.</returns>
        List<Models.Dataverse.Duty> GetFinished();
    }

    public class Duty : IDataverseDuty
    {
        private readonly ILogger<Duty> _logger;
        private readonly IDataverseService _service;

        private readonly string prefix;
        private readonly string tableName;
        private readonly string[] columns;

        public Duty() { }

        public Duty(ILogger<Duty> logger, IDataverseService service, Config config)
        {
            _logger = logger;
            _service = service;
            prefix = config.GetValue("Dataverse:Prefix");
            tableName = config.GetValue("Dataverse:DutyTable");
            columns = new string[] { $"{prefix}crewlist", $"{prefix}end", $"{prefix}flightlist", $"{prefix}hash", $"{prefix}label", $"{prefix}merlotid", $"{prefix}portlist", $"{prefix}start", $"{prefix}{tableName}id" };
        }

        public Models.Dataverse.Duty Get(string id)
        {
            ConditionExpression condition = new()
            {
                AttributeName = $"{prefix}merlotid",
                Operator = ConditionOperator.Equal,
                Values = { id }
            };

            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            query.Criteria.AddCondition(condition);

            // need to use RetrieveMultiple since that's the only Retrieve method that allows search criteria
            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return null;

            return new Models.Dataverse.Duty(response.Entities.First(), prefix, tableName);
        }

        public List<Models.Dataverse.Duty> GetMultiple(IEnumerable<string> ids)
        {
            var batches = ids.Chunk(1000);
            List<Models.Dataverse.Duty> result = new();

            foreach (var batch in batches)
            {
                ConditionExpression condition = new($"{prefix}merlotid", ConditionOperator.In, batch);

                QueryExpression query = new($"{prefix}{tableName}");
                query.ColumnSet.AddColumns(columns);
                query.Criteria.AddCondition(condition);

                var response = _service.GetClient().RetrieveMultiple(query);

                if (response.Entities.Count > 0)
                    result.AddRange(response.Entities.Select(e => new Models.Dataverse.Duty(e, prefix, tableName)));
            }

            return result;
        }

        public Guid Create(Models.Dataverse.Duty duty)
        {
            Microsoft.Xrm.Sdk.Entity entity = duty.ToEntity(tableName, prefix);

            return _service.GetClient().Create(entity);
        }

        public List<Guid> CreateMultiple(IEnumerable<Models.Dataverse.Duty> duties)
        {
            var batches = duties.Chunk(1000);
            List<Guid> results = new();

            foreach (var batch in batches)
            {
                List<Microsoft.Xrm.Sdk.Entity> entities = batch.Select(duty => duty.ToEntity(tableName, prefix)).ToList();

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
                _logger.LogInformation($"Created {results.Count} entities.");
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

        public void Update(Models.Dataverse.Duty duty)
        {
            Microsoft.Xrm.Sdk.Entity entity = duty.ToUpdateEntity(tableName, prefix);

            _service.GetClient().Update(entity);
        }

        public void UpdateMultiple(IEnumerable<Models.Dataverse.Duty> duties)
        {
            var batches = duties.Chunk(1000);

            foreach (var batch in batches)
            {
                List<Microsoft.Xrm.Sdk.Entity> entities = batch.Select(duty => duty.ToUpdateEntity(tableName, prefix)).ToList();

                ExecuteMultipleRequest requests = new()
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = false },
                    Requests = new OrganizationRequestCollection()
                };

                requests.Requests.AddRange(entities.Select(e => new UpdateRequest() { Target = e }));
                _service.GetClient().Execute(requests);

                _logger.LogInformation($"Updated {entities.Count} entities.");
            }
        }

        public Guid? Upsert(Models.Dataverse.Duty duty)
        {
            var entity = Get(duty.MerlotId);
            if (entity is null)
                return Create(duty);
            else
                Update(duty);
            return null;
        }

        public List<Guid> UpsertMultiple(IEnumerable<Models.Dataverse.Duty> duties)
        {
            List<Guid> responses = new();

            var entities = GetMultiple(duties.Select(d => d.MerlotId));

            var toCreate = duties.Where(d => entities.All(e => e.MerlotId != d.MerlotId && !d.Id.HasValue)).ToList();
            var toUpdate = entities.Select(e =>
            {
                var duty = duties.Where(d => d.MerlotId.Equals(e.MerlotId) || d.Id.Equals(e.Id)).FirstOrDefault();
                if (duty is not null && !duty.GetHash().Equals(e.Hash))
                {
                    duty.Id = e.Id;
                    return duty;
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

            _logger.LogInformation($"{toUpdate.Count} updated, {toCreate.Count} new records");

            if (responses.Count > 0)
                return responses;

            return new List<Guid>();
        }

        public List<Models.Dataverse.Duty> GetByCrew(string id, string search = null)
        {
            DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            ConditionExpression condition = new($"{prefix}crewlist", ConditionOperator.Like, $"%{id}%");
            FilterExpression filter = new(LogicalOperator.Or);
            filter.AddCondition(new($"{prefix}start", ConditionOperator.OnOrAfter, today));
            filter.AddCondition(new($"{prefix}end", ConditionOperator.OnOrAfter, today));

            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            query.Criteria.AddCondition(condition);
            query.Criteria.AddFilter(filter);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query.Criteria.AddCondition(new ConditionExpression(
                    $"{prefix}label", ConditionOperator.Like, $"%{search}%"
                ));
            }

            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return new List<Models.Dataverse.Duty>();

            return response.Entities.Select(e => new Models.Dataverse.Duty(e, prefix, tableName)).Where(d => d.Crew.Contains(id)).ToList();
        }

        public List<Models.Dataverse.Duty> GetFinished()
        {
            ConditionExpression condition = new();
            condition.AttributeName = $"{prefix}end";
            condition.Operator = ConditionOperator.OnOrBefore;
            condition.Values.Add(DateTime.UtcNow.AddHours(-48));

            QueryExpression query = new($"{prefix}{tableName}");
            query.ColumnSet.AddColumns(columns);
            query.Criteria.AddCondition(condition);

            var response = _service.GetClient().RetrieveMultiple(query);

            if (response.Entities.Count == 0)
                return new List<Models.Dataverse.Duty>();

            return response.Entities.Select(e => new Models.Dataverse.Duty(e, prefix, tableName)).ToList();
        }
    }
}
