using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AdaptiveCards;
using KTI.PAL.Teams.Core.Models.PAL;
using KTI.PAL.Teams.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KTI.PAL.Teams.Functions
{
    public class MessagingExtensions
    {
        private readonly ILogger _logger;
        private Config _config;
        private Core.Domains.Dataverse.IDataverseDuty _duties;
        private Core.Domains.Dataverse.IDataverseCrew _crew;
        private Core.Domains.Graph.IGraphUserDomain _users;
        private HttpClient _client;
        private TimeZoneInfo tzi;

        public MessagingExtensions(ILogger<MessagingExtensions> logger, Core.Domains.Dataverse.IDataverseDuty duties, Core.Domains.Dataverse.IDataverseCrew crew, Core.Domains.Graph.IGraphUserDomain users, Config config)
        {
            _logger = logger;
            _duties = duties;
            _crew = crew;
            _users = users;
            _config = config;
            _client = new HttpClient();
        }

        [Function("extensions")]
        public async Task<HttpResponseData> Extensions([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            string requestJson = await req.ReadAsStringAsync();
            Microsoft.Bot.Schema.Activity activity = JsonConvert.DeserializeObject<Activity>(requestJson);

            Microsoft.Bot.Schema.Entity clientInfo = activity.Entities.Where(e => e.Type.Equals("clientInfo")).First();
            // tzi = TimeZoneInfo.FindSystemTimeZoneById(((string)clientInfo.Properties["timezone"]));

            switch (activity.Name)
            {
                case "composeExtension/query":
                    MessagingExtensionQuery query = JsonConvert.DeserializeObject<MessagingExtensionQuery>(activity.Value.ToString());
                    return await HandleQuery(req, activity, query);

                // case "composeExtension/submitAction":
                //     MessagingExtensionAction action = JsonConvert.DeserializeObject<MessagingExtensionAction>(activity.Value.ToString());
                //     return await HandleAction(req, activity, action);

                case "composeExtension/fetchTask":
                    MessagingExtensionAction task = JsonConvert.DeserializeObject<MessagingExtensionAction>(activity.Value.ToString());
                    return await HandleFetchTask(req, activity, task);

                default:
                    _logger.LogInformation($"Unhandled activity '{activity.Name}'\n{JsonConvert.SerializeObject(activity)}");
                    return req.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        [Function("extensions/submit")]
        public async Task<HttpResponseData> ProcessSubmit([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            string requestJson = await req.ReadAsStringAsync();

            var objDefinition = new { key = "", content = "", header = "" };
            var definition = new { initiator = "", offer = objDefinition, swapWith = objDefinition, receive = objDefinition };
            var swap = JsonConvert.DeserializeAnonymousType(requestJson, definition);

            var initiator = _crew.Get(AadUserId: swap.initiator);
            var approver = _crew.Get(AadUserId: _config.GetValue("Teams:SchedulingManager"));
            var swapWith = _crew.Get(employeeId: swap.swapWith.key, email: swap.swapWith.content);

            var offer = _duties.Get(swap.offer.key);
            var receive = _duties.Get(swap.receive.key);

            SwapRequest request = new SwapRequest
            {
                InitiatorId = Guid.Parse(initiator.AadUserId),
                InitiatorEmpCode = initiator.EmployeeId,

                ReceiverId = Guid.Parse(swapWith.AadUserId),
                ReceiverEmpCode = swapWith.EmployeeId,

                ApproverId = Guid.Parse(approver.AadUserId),

                OfferDuty = int.Parse(offer.MerlotId),
                OfferDutyLabel = offer.Label,
                ReceiveDuty = int.Parse(receive.MerlotId),
                ReceiveDutyLabel = receive.Label
            };

            HttpResponseMessage approvalResponse = await _client.PostAsJsonAsync(_config.GetValue("App:Flow"), request);
            approvalResponse.EnsureSuccessStatusCode();

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }


        private async Task<HttpResponseData> HandleQuery(HttpRequestData req, Microsoft.Bot.Schema.Activity activity, Microsoft.Bot.Schema.Teams.MessagingExtensionQuery query)
        {
            // try to get employee id from database
            Core.Models.Dataverse.Crew crew = _crew.Get(AadUserId: activity.From.AadObjectId);
            if (crew is null)
            {
                _logger.LogWarning(new ArgumentNullException(nameof(crew)), $"Cannot fetch Merlot employee code for user {activity.From.Name} (aad id {activity.From.AadObjectId}): record does not exist in database.");
                var nresponse = req.CreateResponse(HttpStatusCode.OK);
                var attachment = new MessagingExtensionAttachment()
                {
                    ContentType = ThumbnailCard.ContentType,
                    Content = new ThumbnailCard
                    {
                        Title = "No duties found.",
                        // Subtitle = "This user does not have any duties returned by Merlot.",
                        Text = "There is no data returned by Merlot associated with this user."
                    }
                };
                attachment.Preview = ((ThumbnailCard)attachment.Content).ToAttachment();

                await nresponse.WriteAsJsonAsync(new MessagingExtensionResponse()
                {
                    ComposeExtension = new MessagingExtensionResult
                    {
                        Type = "result",
                        AttachmentLayout = "list",
                        Attachments = new List<MessagingExtensionAttachment>() { attachment }
                    }
                });

                return nresponse;
            }

            var duties = _duties.GetByCrew(crew.EmployeeId);

            if (query.Parameters.Where(p => p.Name.Equals("initialRun") && p.Value.Equals("true")).Any())
            {
                duties = duties.Take(20).ToList();
            }
            else if (query.Parameters.Any(p => p.Name.Equals("scheduleQuery")))
            {
                string value = query.Parameters.First(p => p.Name.Equals("scheduleQuery")).Value.ToString();
                duties = duties.Where(d => d.Label.Contains(value) || d.Ports.Any(p => p.Contains(value))).ToList();
            }

            List<MessagingExtensionAttachment> attachments = new();
            duties.ForEach(d =>
            {
                MessagingExtensionAttachment attachment = new()
                {
                    ContentType = ThumbnailCard.ContentType,
                    Content = new ThumbnailCard
                    {
                        Title = d.Label,
                        Subtitle = string.Join(", ", d.Ports),
                        Text = $"{d.Start} to {d.End}"
                        // Text = $"{TimeZoneInfo.ConvertTime(d.Start, tzi)} to {TimeZoneInfo.ConvertTime(d.End, tzi)}"
                    }
                };
                attachment.Preview = ((ThumbnailCard)attachment.Content).ToAttachment();
                attachments.Add(attachment);
            });

            var result = new MessagingExtensionResponse()
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = attachments
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);

            return response;
        }

        private async Task<HttpResponseData> HandleFetchTask(HttpRequestData req, Microsoft.Bot.Schema.Activity activity, Microsoft.Bot.Schema.Teams.MessagingExtensionAction action)
        {
            // string json;
            // using (StreamReader r = new StreamReader("./Cards/DynamicSwap.json"))
            // {
            //     json = r.ReadToEnd();
            // }

            MessagingExtensionActionResponse result = new MessagingExtensionActionResponse()
            {
                Task = new TaskModuleContinueResponse
                {
                    Value = new TaskModuleTaskInfo()
                    {
                        Width = "large",
                        Height = "medium",
                        Title = "Exchanges",
                        Url = $"{_config.GetValue("Azure:Storage")}/extensions/swap.html",
                        CompletionBotId = _config.GetValue("Azure:MessagingExtensionBotId")
                    }
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);

            return response;
        }
    }
}
