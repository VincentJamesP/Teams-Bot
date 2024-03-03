using System.Collections.Generic;
using System.Linq;
using Microsoft.Graph;

namespace KTI.PAL.Teams.Core.Models
{
    public static class Extensions
    {
        /// <summary>
        /// Convert a Dataverse Flight record into a generic XRM entity.
        /// </summary>
        /// <param name="flight">The flight record to convert.</param>
        /// <param name="tableName">The name of the table where the record will be added to.</param>
        /// <param name="prefix">The prefix of the table where the record will be added to.</param>
        /// <returns>A generic entity representation of the flight.</returns>
        public static Microsoft.Xrm.Sdk.Entity ToEntity(this Models.Dataverse.Flight flight, string tableName, string prefix)
        {
            var entity = new Microsoft.Xrm.Sdk.Entity()
            {
                LogicalName = $"{prefix}{tableName}",
                Attributes = new()
                {
                    { $"{prefix}operatingcrew", string.Join(',', flight.OperatingCrew.Select(c => $"{c.empCode};{string.Join(':', c.roles)}")) },
                    { $"{prefix}flightnumber", flight.FlightNumber },
                    { $"{prefix}followid", flight.FollowId.ToString() },
                    { $"{prefix}lastmerlotupdate", flight.LastMerlotUpdate },
                    { $"{prefix}teamid", flight.TeamId },
                    { $"{prefix}eventid", flight.EventId }
                }
            };
            if (flight.ScheduledArrival.HasValue)
                entity.Attributes.Add($"{prefix}scheduledarrival", flight.ScheduledArrival.Value);

            if (flight.ScheduledDeparture.HasValue)
                entity.Attributes.Add($"{prefix}scheduleddeparture", flight.ScheduledDeparture.Value);

            if (flight.NonOperatingCrew.Count() > 0)
                entity.Attributes.Add($"{prefix}nonoperatingcrew", string.Join(',', flight.NonOperatingCrew.Select(c => $"{c.empCode};{string.Join(':', c.roles)}")));

            return entity;
        }

        /// <summary>
        /// Convert a Dataverse Flight record into a generic XRM entity, adding the system-generated ID for update purposes.
        /// </summary>
        /// <param name="flight">The flight record to convert.</param>
        /// <param name="tableName">The name of the table where the record will be added to.</param>
        /// <param name="prefix">The prefix of the table where the record will be added to.</param>
        /// <returns>A generic entity representation of the flight.</returns>
        public static Microsoft.Xrm.Sdk.Entity ToUpdateEntity(this Models.Dataverse.Flight flight, string tableName, string prefix)
        {
            var entity = flight.ToEntity(tableName, prefix);
            entity.Attributes.Add($"{prefix}{tableName}id", flight.Id);
            return entity;
        }

        /// <summary>
        /// Convert a Merlot Flight into a Team object, with default channels set up.
        /// </summary>
        /// <param name="flight">The flight information to convert into a team.</param>
        /// <param name="owner">The ID of the owner of the team.</param>
        /// <returns>A Team representation of the flight.</returns>
        public static Microsoft.Graph.Team ToTeam(this Models.Merlot.Flight flight, string owner)
        {
            string name = $"{flight.designatorCode}{flight.flightNumber} {flight.scheduledDeparture.ToString("dd-MM-yyyy")}";
            string description = $"Team for Flight {flight.designatorCode}{flight.flightNumber} {flight.departurePort} to {flight.arrivalPort} ({flight.departurePortIcao} to {flight.arrivalPortIcao}).";
            var team = new Team
            {
                DisplayName = name,
                Description = description,
                Members = new TeamMembersCollectionPage()
                {
                    new AadUserConversationMember
                    {
                        UserId = owner,
                        Roles = new string[] {"owner"},
                        AdditionalData = new Dictionary<string, object>()
                            { {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{owner}')"} }
                    }
                },
                Channels = new TeamChannelsCollectionPage {
                    new Channel{
                        DisplayName = "Pilots",
                        Description = ""
                    },
                    new Channel{
                        DisplayName = "Cabin Crew",
                        Description = ""
                    },
                    new Channel{
                        DisplayName = "Flight Services",
                        Description = ""
                    },
                    new Channel{
                        DisplayName = "Flight Operations",
                        Description = ""
                    }
                },
                AdditionalData = new Dictionary<string, object>()
                { {"template@odata.bind", "https://graph.microsoft.com/v1.0/teamsTemplates('standard')"} }
            };

            return team;
        }

        /// <summary>
        /// Convert a Dataverse Crew record into a AadUserConversationMember to be added to a team.
        /// </summary>
        /// <param name="crew">The crew record to convert.</param>
        /// <returns>An AadUserConversationMember referring to the specified crew.</returns>
        public static Microsoft.Graph.AadUserConversationMember ToTeamMember(this Models.Dataverse.Crew crew)
        {
            return new AadUserConversationMember
            {
                Roles = new string[] { "member" },
                AdditionalData = new Dictionary<string, object>() {
                    {"user@odata.bind", $"https://graph.microsoft.com/v1.0/users('{crew.AadUserId}')"}
                }
            };
        }

        /// <summary>
        /// Convert a Merlot Pairing into an Outlook event.
        /// </summary>
        /// <param name="pairing">The pairing from Merlot to convert.</param>
        /// <returns>An Event object representation of the Pairing.</returns>
        public static Microsoft.Graph.Event ToEvent(this Models.Merlot.Pairing pairing)
        {
            return new Event
            {
                AllowNewTimeProposals = false,
                ResponseRequested = false,
                TransactionId = pairing.id.ToString(),
                Start = new DateTimeTimeZone { DateTime = pairing.startDate.ToString("O"), TimeZone = "UTC" },
                End = new DateTimeTimeZone { DateTime = pairing.endDate.ToString("O"), TimeZone = "UTC" },
                Subject = $"{pairing.pairingWorkType}",
                Body = new ItemBody { ContentType = BodyType.Text, Content = $"{pairing.pairingWorkType} {pairing.label} (pairing id: {pairing.id})" },
                Location = new Location { DisplayName = $"{pairing.duties.First().fromPort}" },

                Attendees = new List<Attendee>()
            };
        }

        /// <summary>
        /// Convert a Merlot Flight information into an Outlook event.
        /// </summary>
        /// <param name="flight">The flight from Merlot to convert.</param>
        /// <returns>An Event object representation of the Flight.</returns>
        public static Microsoft.Graph.Event ToEvent(this Models.Merlot.Flight flight)
        {
            return new Event
            {
                AllowNewTimeProposals = false,
                ResponseRequested = false,
                TransactionId = flight.followId.ToString(),
                Start = new DateTimeTimeZone { DateTime = flight.scheduledDeparture.ToString("O"), TimeZone = "UTC" },
                End = new DateTimeTimeZone { DateTime = flight.scheduledArrival.ToString("O"), TimeZone = "UTC" },
                Subject = $"{flight.designatorCode}{flight.flightNumber}",
                Body = new ItemBody { ContentType = BodyType.Text, Content = $"Flight {flight.designatorCode}{flight.flightNumber}\n {flight.departurePort} to {flight.arrivalPort}\n {flight.scheduledDeparture.ToShortTimeString()} to {flight.scheduledArrival.ToShortTimeString()}" },
                Location = new Location { DisplayName = $"{flight.departurePort}-{flight.arrivalPort}" },

                Attendees = new List<Attendee>()
            };
        }

        /// <summary>
        /// Convert a Merlot FlightCrew into a calendar Attendee.
        /// </summary>
        /// <param name="crew">The flight crew information to convert.</param>
        /// <returns>An Attendee representation of the FlightCrew.</returns>
        public static Microsoft.Graph.Attendee ToAttendee(this Models.Merlot.FlightCrew crew)
        {
            return new Attendee
            {
                EmailAddress = new EmailAddress { Address = crew.emailAddress, Name = crew.knowAs },
                Type = AttendeeType.Required
            };
        }

        /// <summary>
        /// Convert a Dataverse Crew record into a calendar Attendee.
        /// </summary>
        /// <param name="crew">The crew record to convert.</param>
        /// <returns>An Attendee representation of the Crew record.</returns>
        public static Microsoft.Graph.Attendee ToAttendee(this Models.Dataverse.Crew crew)
        {
            return new Attendee
            {
                EmailAddress = new EmailAddress { Address = crew.Email, Name = crew.Name },
                Type = AttendeeType.Required
            };
        }

        /// <summary>
        /// Convert a Dataverse Crew record into a generic XRM entity.
        /// </summary>
        /// <param name="crew">The crew record to convert.</param>
        /// <param name="tableName">The name of the table where the record will be added to.</param>
        /// <param name="prefix">The prefix of the table where the record will be added to.</param>
        /// <returns>A generic entity representation of the Crew record.</returns>
        public static Microsoft.Xrm.Sdk.Entity ToEntity(this Core.Models.Dataverse.Crew crew, string tableName, string prefix)
        {
            return new Microsoft.Xrm.Sdk.Entity()
            {
                LogicalName = $"{prefix}{tableName}",
                Attributes = new()
                {
                    { $"{prefix}employeeid", crew.EmployeeId },
                    { $"{prefix}rank", crew.Rank },
                    { $"{prefix}aaduserid", crew.AadUserId },
                    { $"{prefix}email", crew.Email },
                    { $"{prefix}name", crew.Name }
                }
            };
        }

        /// <summary>
        /// Convert a Dataverse Crew record into a generic XRM entity, adding the system-generated ID for update purposes.
        /// </summary>
        /// <param name="crew">The crew record to convert.</param>
        /// <param name="tableName">The name of the table where the record will be added to.</param>
        /// <param name="prefix">The prefix of the table where the record will be added to.</param>
        /// <returns>A generic entity representation of the Crew record.</returns>
        public static Microsoft.Xrm.Sdk.Entity ToUpdateEntity(this Core.Models.Dataverse.Crew crew, string tableName, string prefix)
        {
            var entity = crew.ToEntity(tableName, prefix);
            entity.Attributes.Add($"{prefix}{tableName}id", crew.Id);
            return entity;
        }

        /// <summary>
        /// Convert a Dataverse Duty record into a generic XRM entity.
        /// </summary>
        /// <param name="duty">The duty record to convert.</param>
        /// <param name="tableName">The name of the table where the record will be added to.</param>
        /// <param name="prefix">The prefix of the table where the record will be added to.</param>
        /// <returns>A generic entity representation of the Duty record.</returns>
        public static Microsoft.Xrm.Sdk.Entity ToEntity(this Core.Models.Dataverse.Duty duty, string tableName, string prefix)
        {
            return new Microsoft.Xrm.Sdk.Entity()
            {
                LogicalName = $"{prefix}{tableName}",
                Attributes = new()
                {
                    // { $"{prefix}flightdutyid", duty.Id},
                    { $"{prefix}merlotid", duty.MerlotId },
                    { $"{prefix}label", duty.Label },
                    { $"{prefix}start", duty.Start },
                    { $"{prefix}end", duty.End },
                    { $"{prefix}flightlist", string.Join(',', duty.Flights) },
                    { $"{prefix}crewlist", string.Join(',', duty.Crew) },
                    { $"{prefix}portlist", string.Join(',', duty.Ports) },
                    { $"{prefix}hash", duty.GetHash() }
                }
            };
        }

        /// <summary>
        /// Convert a Dataverse Duty record into a generic XRM entity, adding the system-generated ID for update purposes.
        /// </summary>
        /// <param name="duty">The duty record to convert.</param>
        /// <param name="tableName">The name of the table where the record will be added to.</param>
        /// <param name="prefix">The prefix of the table where the record will be added to.</param>
        /// <returns>A generic entity representation of the Duty record.</returns>
        public static Microsoft.Xrm.Sdk.Entity ToUpdateEntity(this Core.Models.Dataverse.Duty duty, string tableName, string prefix)
        {
            var entity = duty.ToEntity(tableName, prefix);
            entity.Attributes.Add($"{prefix}{tableName}id", duty.Id);
            return entity;
        }
    }
}
