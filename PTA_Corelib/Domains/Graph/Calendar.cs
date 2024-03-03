using System;
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
    public interface IGraphCalendarDomain
    {
        /// <summary>
        /// Get a single event from Graph.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="userId">An optional user id. If not provided, will fallback to the service user's ID.</param>
        /// <returns>An awaitable Task containing the event whose ID matches the provided ID.</returns>
        Task<Event> Get(string eventId, string userId = null);
        /// <summary>
        /// Create an event on a user's calendar.
        /// </summary>
        /// <param name="item">The calendar event to create.</param>
        /// <param name="userId">The user id whose calendar should be used. If not provided, will fallback to the service user's ID.</param>
        /// <returns>An awaitable Task containing the created event.</returns>
        Task<Event> Create(Event item, string userId = null);
        /// <summary>
        /// Update an event on a user's calendar.
        /// </summary>
        /// <param name="item">The event to update. The event ID should not be null.</param>
        /// <param name="userId">The user id whose calendar was used. If not provided, will fallback to the service user's ID.</param>
        /// <returns>An awaitable Task containing the updated event.</returns>
        Task<Event> Update(Event item, string userId = null);
        /// <summary>
        /// Remove an event from a user's calendar.
        /// </summary>
        /// <param name="eventId">The event ID to remove.</param>
        /// <param name="userId">The user id whose calendar was used. If not provided, will fallback to the service user's ID.</param>
        /// <returns>An awaitable Task.</returns>
        Task Delete(string eventId, string userId = null);
        /// <summary>
        /// Create multiple calendar events.
        /// </summary>
        /// <param name="events">A list of events to create.</param>
        /// <returns>An awaitable Task containing the list of created events.</returns>
        Task<List<Event>> CreateMultiple(IEnumerable<Event> events);
        /// <summary>
        /// Update multiple calendar events.
        /// </summary>
        /// <param name="events">A list of events to update.</param>
        /// <returns>An awaitable Task containing the list of updated events.</returns>
        Task<List<Event>> UpdateMultiple(IEnumerable<Event> events);
        /// <summary>
        /// Cancel multiple calendar events.
        /// </summary>
        /// <param name="eventsIds">A list of events to cancel.</param>
        /// <returns>An awaitable Task.</returns>
        Task CancelMultiple(IEnumerable<string> eventsIds);
    }

    public class Calendar : IGraphCalendarDomain
    {
        private readonly string serviceUser;
        private readonly ILogger<Calendar> _logger;
        private readonly IGraphService _service;
        public Calendar() { }

        public Calendar(ILogger<Calendar> logger, IGraphService service, Config config)
        {
            _logger = logger;
            _service = service;
            serviceUser = config.GetValue("Azure:ServiceUser");
        }

        public async Task<Event> Get(string eventId, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                userId = serviceUser;

            return await _service.GetClient().Users[userId].Calendar.Events[eventId].Request().GetAsync();
        }

        public async Task<Event> Create(Event item, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                userId = serviceUser;

            return await _service.GetClient().Users[userId].Calendar.Events.Request().AddAsync(@item);
        }

        public async Task Delete(string eventId, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                userId = serviceUser;

            await _service.GetClient().Users[userId].Calendar.Events[eventId].Request().DeleteAsync();
        }

        public async Task<Event> Update(Event item, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                userId = serviceUser;

            return await _service.GetClient().Users[userId].Calendar.Events[item.Id].Request().UpdateAsync(item);
        }

        public async Task<List<Event>> CreateMultiple(IEnumerable<Event> events)
        {
            if (events.Count() == 0)
                return new List<Event>();

            List<Event> e = new();
            var b = 0;

            var batches = events.Chunk(4);
            _logger.LogInformation($"Splitting create multiple events to {batches.Count()} batches.");

            foreach (var batch in batches)
            {
                b++;
                BatchRequestContent request = new();
                List<string> requestIds = new();

                foreach (var item in batch)
                {
                    var user = item.Attendees.Count() != 1 ? serviceUser : item.Attendees.First().EmailAddress.Address;

                    var req = _service.GetClient().Users[user].Calendar.Events.Request().GetHttpRequestMessage();
                    req.Method = HttpMethod.Post;
                    req.Content = _service.GetClient().HttpProvider.Serializer.SerializeAsJsonContent(item);
                    requestIds.Add(request.AddBatchRequestStep(req));
                }

                BatchResponseContent response = await _service.GetClient().Batch.Request().PostAsync(request);

                foreach (string id in requestIds)
                {
                    var eventResponse = await response.GetResponseByIdAsync(id);
                    try
                    {
                        var content = JsonConvert.DeserializeAnonymousType(await eventResponse.Content.ReadAsStringAsync(), new { error = new { code = "", message = "" } });

                        if (eventResponse.StatusCode.Equals(HttpStatusCode.BadRequest) && content.error.code.Equals("ErrorDuplicateTransactionId"))
                        { }
                        else
                        {
                            eventResponse.EnsureSuccessStatusCode();
                            var ev = await response.GetResponseByIdAsync<Event>(id);
                            e.Add(ev);
                        }
                    }
                    catch (Microsoft.Graph.ServiceException exception)
                    {
                        Console.WriteLine(JsonConvert.SerializeObject(eventResponse.Content.ReadAsStringAsync()));
                        _logger.LogError(exception, $"Exception in batch {b}: {exception.Message}");
                    }
                }
            }

            return e;
        }

        public async Task<List<Event>> UpdateMultiple(IEnumerable<Event> events)
        {
            // _logger.LogInformation(JsonConvert.SerializeObject(events));
            // return new List<Event>();

            List<Event> e = new();
            var batches = events.Chunk(4);

            foreach (var batch in batches)
            {
                BatchRequestContent request = new();
                List<string> requestIds = new();
                foreach (var item in batch)
                {
                    var user = item.Attendees.Count() == 1 ? serviceUser : item.Attendees.First().EmailAddress.Address;

                    var req = _service.GetClient().Users[user].Calendar.Events[item.Id].Request().GetHttpRequestMessage();
                    req.Method = HttpMethod.Patch;
                    req.Content = _service.GetClient().HttpProvider.Serializer.SerializeAsJsonContent(item);
                    requestIds.Add(request.AddBatchRequestStep(req));
                }

                BatchResponseContent response = await _service.GetClient().Batch.Request().PostAsync(request);

                foreach (string id in requestIds)
                {
                    try
                    {
                        e.Add(await response.GetResponseByIdAsync<Event>(id));
                    }
                    catch (Microsoft.Graph.ServiceException exception)
                    {
                        _logger.LogError(exception, exception.Message);
                    }
                }
            }

            return e;
        }

        public async Task CancelMultiple(IEnumerable<string> eventIds)
        {
            if (eventIds.Count() == 0)
                return;

            var batches = eventIds.Chunk(4);

            foreach (var batch in batches)
            {
                BatchRequestContent request = new();
                List<string> requestIds = new();
                foreach (var id in batch)
                {
                    var req = _service.GetClient().Users[serviceUser].Calendar.Events[id].Cancel().Request().GetHttpRequestMessage();
                    req.Method = HttpMethod.Post;
                    req.Content = _service.GetClient().HttpProvider.Serializer.SerializeAsJsonContent(new { Comment = "This event has been cancelled." });
                    requestIds.Add(request.AddBatchRequestStep(req));
                }

                BatchResponseContent response = await _service.GetClient().Batch.Request().PostAsync(request);

                foreach (string id in requestIds)
                {
                    try
                    {
                        (await response.GetResponseByIdAsync(id)).EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException exception)
                    {
                        _logger.LogError(exception, exception.Message);
                    }
                    catch (Microsoft.Graph.ServiceException exception)
                    {
                        _logger.LogError(exception, exception.Message);
                    }
                }
            }
        }
    }
}
