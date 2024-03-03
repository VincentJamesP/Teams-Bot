using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Models.Merlot
{
    /// <summary>
    /// Class object representation for flight information returned by Merlot.
    /// </summary>
    public class Flight
    {
        public DateTime? originalSTD { get; set; }
        public string arrivalBay { get; set; }
        public string arrivalGate { get; set; }
        public string arrivalPort { get; set; }
        public string arrivalPortIcao { get; set; }

        [JsonProperty("ata")]
        public DateTime? actualArrival { get; set; }
        [JsonProperty("atd")]
        public DateTime? actualDeparture { get; set; }
        public bool cancelled { get; set; }
        public List<FlightCrew> crew { get; set; }
        public List<FlightDelay> delays { get; set; }
        public string departureBay { get; set; }
        public DateTime? departureDate { get; set; }
        public string departureGate { get; set; }
        public string departurePort { get; set; }
        public string departurePortIcao { get; set; }
        public string designatorCode { get; set; }
        public string equipmentRego { get; set; }
        public string equipmentType { get; set; }
        [JsonProperty("eta")]
        public DateTime? estimatedArrival { get; set; }
        [JsonProperty("etd")]
        public DateTime? estimatedDeparture { get; set; }
        public string externalReference { get; set; }
        [JsonProperty("flight")]
        public string flightNumber { get; set; }
        [JsonProperty("flightFollowId")]
        public int followId { get; set; }
        public List<FlightSSR> flightSSRs { get; set; }
        public int? leg { get; set; }
        public int? nextFlightFollowId { get; set; }
        public string Operator { get; set; }
        public List<FlightPaxNumber> paxNumbers { get; set; }
        public bool rescheduled { get; set; }
        public bool retime { get; set; }
        [JsonProperty("sta")]
        public DateTime scheduledArrival { get; set; }
        [JsonProperty("std")]
        public DateTime scheduledDeparture { get; set; }
        public string suffix { get; set; }
        [JsonProperty("times")]
        public List<FlightFollowTime> flightFollowTimes { get; set; }
        public DateTime UpdatedDate { get; set; }

        public string GetHash()
        {
            string str = $"{flightNumber}{followId}{scheduledDeparture}{scheduledArrival}{string.Join(',', crew.Select(c => c.employeeId))}{UpdatedDate}";
            return Core.Common.CalculateHash(str);
        }
    }

    public class FlightCrew
    {
        public string Base { get; set; }
        public string emailAddress { get; set; }
        public string empCode { get; set; }
        public int? employeeId { get; set; }
        public string equipmentGroupCode { get; set; }
        public string equipmentGroupDescription { get; set; }
        public int? equipmentGroupId { get; set; }
        public string equipmentTypeCode { get; set; }
        public int? equipmentTypeId { get; set; }
        public string equipmentTypeName { get; set; }
        public int? flightFollowId { get; set; }
        public List<FlightCrewApproach> flightLogEmployeeApproaches { get; set; }
        public List<FlightCrewFlying> flightLogEmployeeFlyings { get; set; }
        public bool isOperating { get; set; }
        public string knowAs { get; set; }
        [JsonProperty("name1")]
        public string nameFirst { get; set; }
        [JsonProperty("name2")]
        public string nameMiddle { get; set; }
        [JsonProperty("name3")]
        public string nameSuffix { get; set; }
        [JsonProperty("name4")]
        public string nameLast { get; set; }
        public string rank { get; set; }
        public string rankCode { get; set; }
        public int? rankId { get; set; }
        public string rankName { get; set; }
        public List<FlightCrewRole> roles { get; set; }
        public FlightCrewSeat seat { get; set; }
    }

    public class FlightDelay
    {
        public string code { get; set; }
        public int? delayCodeId { get; set; }
        public string delayComment { get; set; }
        public int? delayId { get; set; }
        public int? delayTypeId { get; set; }
        public string description { get; set; }
        public int? flightFollowId { get; set; }
        public int? minutes { get; set; }
        public string type { get; set; }
    }

    public class FlightCrewApproach
    {
        public int? employeeId { get; set; }
        public int? flightFollowId { get; set; }
        public int? flightLogApproachTypeId { get; set; }
        public string flightLogApproachTypeName { get; set; }
        public int? id { get; set; }
        public double number { get; set; }
    }

    public class FlightSSR
    {
        public string comment { get; set; }
        public int? flightFollowId { get; set; }
    }

    public class FlightPaxNumber
    {
        public string code { get; set; }
        public string description { get; set; }
        public int? flightFollowId { get; set; }
        public double number { get; set; }
    }

    public class FlightFollowTime
    {
        public string code { get; set; }
        public int? id { get; set; }
        public DateTime? time { get; set; }
    }

    public class FlightCrewFlying
    {
        public int? employeeId { get; set; }
        public int? flightFollowId { get; set; }
        public int? flightLogFlyingTypeId { get; set; }
        public string flightLogFlyingTypeName { get; set; }
        public int? id { get; set; }
        public double number { get; set; }
    }

    public class FlightCrewRole
    {
        public int? activeRoleId { get; set; }
        public string code { get; set; }
        public string description { get; set; }
        public int? id { get; set; }
        public int? pairingEmployeeActivityId { get; set; }
    }

    public class FlightCrewSeat
    {
        public string code { get; set; }
        public int? equipmentSeatId { get; set; }
        public int? id { get; set; }
        public string name { get; set; }
        public int? pairingEmployeeActivityId { get; set; }
        public int? seatTypeId { get; set; }
    }
}
