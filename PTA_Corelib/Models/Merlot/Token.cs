using System;
using System.Globalization;
using System.Text.Json;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Core.Models.Merlot
{
    /// <summary>
    /// Class object representation for tokens returned by Merlot.
    /// </summary>
    public class Token
    {
        [JsonProperty("access_token")]
        public string Access { get; set; }
        [JsonProperty("refresh_token")]
        public string Refresh { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }
        [JsonProperty("expires_in")]
        public ushort ExpiresIn { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("display-name")]
        public string DisplayName { get; set; }
        [JsonProperty("user-role")]
        public string UserRole { get; set; }
        [JsonProperty("user-groups")]
        public string UserGroups { get; set; }
        [JsonPropertyAttribute(".issued")]
        public DateTime IssuedOn { get; set; }
        [JsonProperty(".expires")]
        public DateTime ExpiresOn { get; set; }

        public Token() { }

        /// <summary>
        /// Create a Merlot token from JSON.
        /// </summary>
        /// <param name="json">The JSON string to create the token from.</param>
        public Token(string json)
        {
            JsonElement tokenJson = JsonDocument.Parse(json).RootElement;

            this.Access = tokenJson.GetProperty("access_token").GetString();
            this.Refresh = tokenJson.GetProperty("refresh_token").GetString();

            this.TokenType = tokenJson.GetProperty("token_type").GetString();
            this.ExpiresIn = (ushort)tokenJson.GetProperty("expires_in").GetInt16();
            this.Username = tokenJson.GetProperty("username").GetString();
            this.DisplayName = tokenJson.GetProperty("display-name").GetString();
            this.UserRole = tokenJson.GetProperty("user-role").GetString();
            this.UserGroups = tokenJson.GetProperty("user-groups").GetString();
            this.IssuedOn = DateTime.ParseExact(tokenJson.GetProperty(".issued").GetString(), "ddd, dd MMM yyyy HH:mm:ss Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            this.ExpiresOn = DateTime.ParseExact(tokenJson.GetProperty(".expires").GetString(), "ddd, dd MMM yyyy HH:mm:ss Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }
    }
}
