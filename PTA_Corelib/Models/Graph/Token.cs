using System;

namespace KTI.PAL.Teams.Core.Models.Graph
{
    /// <summary>
    /// A basic container class for tokens returned by Microsoft Graph.
    /// </summary>
    public class Token
    {
        public string TokenType { get; set; }
        public string AccessToken { get; set; }
        public DateTimeOffset ExpiresOn { get; set; }
    }
}
