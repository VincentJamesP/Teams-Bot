using System;
using System.Linq;
using KTI.PAL.Teams.Core.Services;
using Microsoft.IdentityModel.Tokens;

namespace KTI.PAL.Teams.Core.Models.Dataverse
{
    public class Crew
    {
        public string AadUserId { get; set; }
        public string Email { get; set; }
        public string EmployeeId { get; set; }
        public string Name { get; set; }
        public string Rank { get; set; }
        public Guid? Id { get; set; }

        public Crew() { }
        public Crew(Microsoft.Xrm.Sdk.Entity entity, string prefix, string table)
        {
            AadUserId = entity.GetAttributeValue<string>($"{prefix}aaduserid");
            Email = entity.GetAttributeValue<string>($"{prefix}email");
            EmployeeId = entity.GetAttributeValue<string>($"{prefix}employeeid");
            Name = entity.GetAttributeValue<string>($"{prefix}name");
            Rank = entity.GetAttributeValue<string>($"{prefix}rank");
            Id = entity.GetAttributeValue<Guid>($"{prefix}{table}id");
        }
        public Crew(Models.Merlot.FlightCrew crew)
        {
            EmployeeId = crew.empCode;
            Rank = crew.rank;
            Email = crew.emailAddress;
            Name = crew.knowAs;
        }

        public Crew(Models.Merlot.Employee employee)
        {
            EmployeeId = employee.empCode;
            Name = employee.knownAs;

            var emails = employee.employeeEmails;
            if (emails is null)
            {
                Email = employee.employeeEmails.FirstOrDefault(e => e.primary).email;
                if (string.IsNullOrWhiteSpace(Email))
                    Email = employee.employeeEmails.First().email;
            }
            // since rank can be better retrieved from elsewhere, setting the Rank will be done in the domain.
            // Rank = employee.employeeRatings.First(r => r.activeFrom < DateTime.UtcNow && r.activeTo > DateTime.UtcNow).rank;
        }
    }
}
