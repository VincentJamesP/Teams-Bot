using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
    public class Api
    {
        private readonly ILogger _logger;
        private Core.Domains.Dataverse.IDataverseDuty _duties;
        private Core.Domains.Dataverse.IDataverseCrew _crew;
        private Core.Domains.Graph.IGraphUserDomain _users;
        private Core.Domains.Merlot.IMerlotPairing _pairings;
        private Core.Domains.Merlot.IMerlotEmployee _employees;

        public Api(ILogger<Api> logger, Core.Domains.Dataverse.IDataverseDuty duties, Core.Domains.Dataverse.IDataverseCrew crew, Core.Domains.Graph.IGraphUserDomain users, Core.Domains.Merlot.IMerlotPairing pairings, Core.Domains.Merlot.IMerlotEmployee employees)
        {
            _logger = logger;
            _duties = duties;
            _crew = crew;
            _users = users;
            _pairings = pairings;
            _employees = employees;
        }

        // [Function("tabs/notifications")]
        // public async Task<HttpResponseData> GetNotifications([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        // {
        //     var queries = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        //     string id = queries["id"];
        //     if (string.IsNullOrWhiteSpace(id))
        //         return req.CreateResponse(HttpStatusCode.BadRequest);

        //     var crew = _crew.Get(AadUserId: id);
        //     var employee = await _employees.Get(crew.EmployeeId);

        //     List<Core.Models.Merlot.Pairing> pairings = new();

        //     if (Cache.pairings is null)
        //     {
        //         DateTime now = DateTime.UtcNow;
        //         DateTime from = new DateTime(now.Year, now.Month, now.Day);
        //         DateTime to = from.AddDays(2);
        //         pairings = await _pairings.GetAsync(from, to, employee.id.ToString());
        //     }
        //     else
        //     {
        //         pairings = Cache.pairings;
        //     }

        //     var userPairings = pairings.Where(p => p.activeFlag.Equals("Active") && p.pairingEmployees.Any(e => e.empCode.Equals(crew.EmployeeId)));

        //     var response = req.CreateResponse(HttpStatusCode.OK);
        //     await response.WriteAsJsonAsync(userPairings);

        //     return response;
        // }

        [Function("tabs/reserve")]
        public async Task<HttpResponseData> GetReserveList([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var queries = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var query = queries["query"];
            var ranks = queries["ranks"];
            var reserve = queries["reserves"];

            List<Core.Models.Merlot.Pairing> pairings = Cache.pairings;

            if (pairings is null)
            {
                DateTime now = DateTime.UtcNow;
                DateTime from = new DateTime(now.Year, now.Month, now.Day);
                DateTime to = from.AddMonths(1);
                to = new DateTime(to.Year, to.Month, 2).AddTicks(-1);
                if ((to - from).TotalDays < 7)
                {
                    to = to.AddMonths(1);
                }
                pairings = await _pairings.GetAsync(from, to);
                Cache.SetPairings(pairings);
            }

            // get all reserve pairings
            var reserves = pairings.Where(p =>
                p.activeFlagId.Equals(2)
             && p.pairingWorkTypeId.Equals(2)
             && !string.IsNullOrWhiteSpace(p.label)
             && p.startDate >= DateTime.UtcNow
            ).SelectMany(p =>
            // and format them to be readable by the frontend
            {
                return p.pairingEmployees.Select(e =>
                   {
                       var reserve = Regex.Match(p.label, @"^(\d?[A-Z]+)(\d+(.+)?)");
                       return new
                       {
                           name = e.name,
                           empCode = e.empCode,
                           rank = e.rank,
                           reserve = reserve.Groups[1].Value,
                           priority = reserve.Groups[2].Value,
                           date = p.startDate,

                           label = p.label
                       };
                   });
            });


            if (!string.IsNullOrWhiteSpace(query))
            {
                reserves = reserves.Where(r =>
                    r.empCode.Contains(query)
                 || r.name.ToLower().Contains(query.ToLower())
                 || r.label.Contains(query)
                );
            }
            if (!string.IsNullOrWhiteSpace(ranks))
            {
                reserves = reserves.Where(r => ranks.Split(',').Contains(r.rank));
            }
            if (!string.IsNullOrWhiteSpace(reserve))
            {
                reserves = reserves.Where(r => reserve.Split(',').Contains(r.reserve));
            }


            var sorted = reserves.ToList();
            // sorted.Sort((a, b) =>
            // {
            //     var ai = int.Parse(Regex.Replace(a.priority, @"\D+", String.Empty));
            //     var bi = int.Parse(Regex.Replace(b.priority, @"\D+", String.Empty));

            //     if (ai < bi) return -1;
            //     if (ai > bi) return 1;
            //     return 0;
            // });
            sorted.Sort((a, b) => DateTime.Compare(a.date, b.date));

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sorted.Select(r => new
            {
                name = r.name,
                empCode = r.empCode,
                rank = r.rank,
                reserve = r.reserve,
                priority = r.priority,
                date = r.date.ToString("dd MMM yyyy"),
                label = r.label
            }));

            return response;
        }

        [Function("extensions/search")]
        public async Task<HttpResponseData> Search([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var queries = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            // var from = query["key"];
            Models.SearchQuery search = new Models.SearchQuery { type = queries["type"], id = queries["id"], empCode = queries["empCode"], query = queries["query"] };

            // Models.SearchQuery search = await req.ReadFromJsonAsync<Models.SearchQuery>();
            if (queries is null)
                return req.CreateResponse(HttpStatusCode.BadRequest);

            var user = _crew.Get(AadUserId: search.id);

            List<Models.SearchResult> results = new();
            if (search.type.Equals("dutiesOf"))
            {
                List<Core.Models.Dataverse.Duty> duties = new();
                if (!string.IsNullOrWhiteSpace(search.empCode))
                {
                    duties = _duties.GetByCrew(search.empCode, search.query);
                }
                else if (user is not null)
                {
                    duties = _duties.GetByCrew(user.EmployeeId, search.query);
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                results = duties.Select(duty => new Models.SearchResult
                {
                    key = duty.MerlotId,
                    content = $"{duty.Start.ToString("ddd dd MMM yyyy").ToUpper()} to {duty.End.ToString("ddd dd MMM yyyy").ToUpper()} | {string.Join(',', duty.Ports)}",
                    header = duty.Label,
                }).ToList();
            }
            else if (search.type.Equals("users"))
            {
                var users = _crew.SearchByName(search.query, user.Rank);
                users.RemoveAll(c => c.EmployeeId.Equals(user.EmployeeId));
                results = users.Select(user => new Models.SearchResult
                {
                    key = user.EmployeeId,
                    content = $"{user.Email}",
                    header = $"{user.Name}"
                }).ToList();
            }
            else if (search.type.Equals("flights"))
            {
                throw new NotImplementedException();
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(results);
            return response;
        }
    }
}
