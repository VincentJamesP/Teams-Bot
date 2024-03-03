using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Graph.TermStore;

namespace KTI.PAL.Teams.Core.Models.Merlot
{
    /// <summary>
    /// Wrapper class for Merlot Pairing API, since Merlot returns the pairings as a value in an object rather than the list directly.
    /// </summary>
    public class PairingResponse
    {
        public List<Pairing> pairings { get; set; }
    }

    /// <summary>
    /// Class object representation for the pairing information returned by Merlot.
    /// </summary>
    public class Pairing : PairingBase
    {
        public string label { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }

        public int activeFlagId { get; set; }
        public string activeFlag { get; set; }
        public int? pairingWorkTypeId { get; set; }
        public string pairingWorkType { get; set; }

        public List<PairingDuty> duties { get; set; }
        public List<PairingEmployee> pairingEmployees { get; set; }
    }

    public class PairingDuty : PairingBase
    {
        public int fromPortId { get; set; }
        public string fromPort { get; set; }
        public int toPortId { get; set; }
        public string toPort { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public List<PDFlight> flights { get; set; }
        public List<PDEvent> events { get; set; }
        public List<PDCourse> courses { get; set; }
        public List<PDAttribute> attributes { get; set; }
    }

    public class PairingEmployee
    {
        public int employeeId { get; set; }
        public string name { get; set; }
        public string empCode { get; set; }
        public string rank { get; set; }
        public List<PEDuty> duties { get; set; }
    }

    public class PDFlight : PairingBase
    {
        public string operationalSuffix { get; set; }
        public string flight { get; set; }
        public DateTime std { get; set; }
        public DateTime sta { get; set; }
        public int dutyOrder { get; set; }
        public bool cancelled { get; set; }
    }

    public class PDEvent
    {
        public int eventTypeId { get; set; }
        public string eventType { get; set; }
        public int fromPortId { get; set; }
        public string fromPort { get; set; }
        public int dutyOrder { get; set; }
    }

    public class PDCourse
    {
        public int courseInstanceId { get; set; }
    }

    public class PDAttribute
    {

    }

    public class PEDuty
    {
        public int pairingDutyId { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public List<PEEvent> events { get; set; }
        public List<PECourse> courses { get; set; }
        public List<PEAttribute> seats { get; set; }
        public List<PEAttribute> roles { get; set; }
    }

    public class PEEvent
    {
        public int pairingEventId { get; set; }
        public string bookingStatus { get; set; }
        public int? bookingStatusId { get; set; }
        public string bookingRef { get; set; }
        public bool isOwnAccommodation { get; set; }
        public int? portAccommodationId { get; set; }
        public string portAccommodationName { get; set; }
    }

    public class PECourse
    {
        public int courseInstanceId { get; set; }
        public int courseId { get; set; }
        public string label { get; set; }
        public bool isInstructor { get; set; }
        public bool isStudent { get; set; }
        public bool isSupportCrew { get; set; }
    }

    public class PEAttribute
    {
        public int id { get; set; }
        public string code { get; set; }
        public int? flightFollowId { get; set; }
        public int? pairingEventId { get; set; }
        public int? courseActivityQualificationEventId { get; set; }
    }

    public class PEAssignmentProperty
    {
        public string code { get; set; }
    }
}