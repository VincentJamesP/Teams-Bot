using System;
using System.Collections.Generic;

using Newtonsoft.Json;


namespace KTI.PAL.Teams.Core.Models.PAL
{
    /// <summary>
    /// Representation of a swap request to be sent to the Approval Flow.
    /// </summary>
    public class SwapRequest
    {
        public Guid InitiatorId { get; set; }
        public string InitiatorEmpCode { get; set; }
        public string InitiatorEmail { get; set; }

        public Guid ReceiverId { get; set; }
        public string ReceiverEmpCode { get; set; }
        public string ReceiverEmail { get; set; }

        public Guid ApproverId { get; set; }
        public string ApproverEmail { get; set; }

        public int OfferDuty { get; set; }
        public string OfferDutyLabel { get; set; }

        public int ReceiveDuty { get; set; }
        public string ReceiveDutyLabel { get; set; }
    }
}
