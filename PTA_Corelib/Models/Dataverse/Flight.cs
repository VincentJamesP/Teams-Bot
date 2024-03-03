using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KTI.PAL.Teams.Core.Models.Merlot;
using KTI.PAL.Teams.Core.Services;
using Microsoft.Graph;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Models.Dataverse
{
    public class Flight
    {
        public string EventId { get; set; }
        public string FlightNumber { get; set; }
        public int FollowId { get; set; }
        public DateTime LastMerlotUpdate { get; set; }
        public List<FlightCrew> NonOperatingCrew { get; set; } = new();
        public List<FlightCrew> OperatingCrew { get; set; } = new();
        public DateTime? ScheduledArrival { get; set; } = null;
        public DateTime? ScheduledDeparture { get; set; } = null;
        public string TeamId { get; set; }
        public Guid? Id { get; set; }

        /// <summary>
        /// Create a hash using sha256 of the flight, using information that is most likely to be updated.
        /// </summary>
        /// <returns>a string representation of the hashed flight.</returns>
        public string GetHash()
        {
            string str = $"{FlightNumber}{FollowId}{ScheduledDeparture}{ScheduledArrival}{string.Join(',', OperatingCrew.Select(c => $"{c.empCode};{string.Join(':', c.roles)}"))}{string.Join(',', NonOperatingCrew.Select(c => $"{c.empCode};{string.Join(':', c.roles)}"))}{LastMerlotUpdate}";
            return Core.Common.CalculateHash(str);
        }

        public Flight() { }
        public Flight(Microsoft.Xrm.Sdk.Entity entity, string prefix, string table)
        {
            Id = entity.GetAttributeValue<Guid>($"{prefix}{table}id");
            FlightNumber = entity.GetAttributeValue<string>($"{prefix}flightnumber");
            FollowId = int.Parse(entity.GetAttributeValue<string>($"{prefix}followid"));
            ScheduledDeparture = entity.GetAttributeValue<DateTime>($"{prefix}scheduleddeparture");
            ScheduledArrival = entity.GetAttributeValue<DateTime>($"{prefix}scheduledarrival");

            var operating = entity.GetAttributeValue<string>($"{prefix}operatingcrew");
            if (!string.IsNullOrWhiteSpace(operating))
                OperatingCrew = operating.Split(',').Select(c => new FlightCrew(c)).ToList();

            var nonoperating = entity.GetAttributeValue<string>($"{prefix}nonoperatingcrew");
            if (!string.IsNullOrWhiteSpace(nonoperating))
                NonOperatingCrew = nonoperating.Split(',').Select(c => new FlightCrew(c)).ToList();

            // CrewIds = entity.GetAttributeValue<string>($"{prefix}crewlist")?.Split(',').ToList();
            TeamId = entity.GetAttributeValue<string>($"{prefix}teamid");
            EventId = entity.GetAttributeValue<string>($"{prefix}eventid");
            LastMerlotUpdate = entity.GetAttributeValue<DateTime>($"{prefix}lastmerlotupdate");
        }
        public Flight(Models.Merlot.Flight flight)
        {
            FlightNumber = $"{flight.designatorCode}{flight.flightNumber}";
            FollowId = flight.followId;
            ScheduledDeparture = flight.scheduledDeparture;
            ScheduledArrival = flight.scheduledArrival;

            OperatingCrew = flight.crew.Where(c => c.roles.Any(r => r.code.Equals("OP"))).Select(c => new FlightCrew(c.empCode, c.roles)).ToList();
            NonOperatingCrew = flight.crew.Where(c => !c.roles.Any(r => r.code.Equals("OP"))).Select(c => new FlightCrew(c.empCode, c.roles)).ToList();

            // CrewIds = flight.crew.Select(c => c.empCode).ToList();
            TeamId = null;
            EventId = null;
            LastMerlotUpdate = flight.UpdatedDate;
        }
    }

    public class FlightCrew
    {
        public string empCode { get; set; }
        public List<string> roles { get; set; }

        public FlightCrew() { }
        public FlightCrew(string empCode, IEnumerable<FlightCrewRole> roles)
        {
            this.empCode = empCode;
            this.roles = roles.Select(r => r.code).ToList();
        }
        public FlightCrew(string s)
        {
            var se = s.Split(';');
            empCode = se[0];
            var sr = se[1].Split(':');
            roles = sr.ToList();
        }
    }
}
