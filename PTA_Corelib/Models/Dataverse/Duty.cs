using System;
using System.Collections.Generic;
using System.Linq;
using KTI.PAL.Teams.Core.Services;

namespace KTI.PAL.Teams.Core.Models.Dataverse
{
    public class Duty
    {
        public List<string> Crew { get; set; }
        public DateTime End { get; set; }
        public List<string> Flights { get; set; }
        public string Hash { get; set; }
        // merlot doesn't provide an api to fetch pairings by id so this is unused
        public string MerlotId { get; set; }
        public string Label { get; set; }
        public List<string> Ports { get; set; }
        public DateTime Start { get; set; }
        public Guid? Id { get; set; }

        public Duty() { }
        public Duty(Core.Models.Merlot.Pairing pairing)
        {
            MerlotId = pairing.id.ToString();
            Label = pairing.label;
            Start = pairing.startDate;
            End = pairing.endDate;
            Flights = pairing.duties.SelectMany(d => d.flights.Select(f => f.id.ToString())).ToList();
            Crew = pairing.pairingEmployees.Select(e => e.empCode).ToList();
            Ports = pairing.duties.SelectMany(d => new string[] { d.fromPort, d.toPort }).Distinct().ToList();
            Hash = GetHash();
        }
        public Duty(Microsoft.Xrm.Sdk.Entity entity, string prefix, string table)
        {
            Crew = entity.GetAttributeValue<string>($"{prefix}crewlist")?.Split(',').ToList();
            End = entity.GetAttributeValue<DateTime>($"{prefix}end");
            Flights = entity.GetAttributeValue<string>($"{prefix}flightlist")?.Split(',').ToList();
            Hash = entity.GetAttributeValue<string>($"{prefix}hash");
            MerlotId = entity.GetAttributeValue<string>($"{prefix}merlotid");
            Label = entity.GetAttributeValue<string>($"{prefix}label");
            Ports = entity.GetAttributeValue<string>($"{prefix}portlist")?.Split(',').ToList();
            Start = entity.GetAttributeValue<DateTime>($"{prefix}start");
            Id = entity.GetAttributeValue<Guid>($"{prefix}{table}id");
        }

        public string GetHash()
        {
            string str = $"{MerlotId}{Label}{Start}{End}{string.Join(',', Flights)}{string.Join(',', Crew)}{string.Join(',', Ports)}";
            return Core.Common.CalculateHash(str);
        }
    }

    public class CrewDuty
    {
        public string Id { get; set; }
        public List<Duty> Duties { get; set; }
    }
}
