using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using KTI.PAL.Teams.Core.Models.Merlot;
using KTI.PAL.Teams.Core.Services;

namespace KTI.PAL.Teams.Core.Domains.Merlot
{
    public interface IMerlotAuthentication
    {
        /// <summary>
        /// A Task signifying that the initial authentication token has already been retrieved.
        /// </summary>
        Task Initialization { get; }
        /// <summary>
        /// Request a new token from Merlot using login details.
        /// </summary>
        /// <param name="username">The merlot username.</param>
        /// <param name="password">The merlot password.</param>
        /// <param name="role">The role to use. Defaults to "User."</param>
        /// <returns>An awaitable Task containing the token returned by Merlot.</returns>
        Task<Token> CreateToken(string username = null, string password = null, string role = "User");
        /// <summary>
        /// Refresh the current token, request a new token from Merlot using the refresh token.
        /// </summary>
        /// <param name="token">The token to be refreshed.</param>
        /// <param name="role">The role to use. Defaults to "User."</param>
        /// <returns>An awaitable Task containing the refreshed tokens.</returns>
        Task<Token> RefreshToken(Token token, string role = "User");
    }

    public class Authentication : IMerlotAuthentication
    {
        public Task Initialization { get; private set; }
        public Core.Models.Merlot.Token tokens { get; set; }

        private ILogger<Authentication> _logger { get; set; }
        private readonly IMerlotService _service;
        private readonly Config _config;

        public Authentication(ILogger<Authentication> logger, IMerlotService service, Config config)
        {
            _logger = logger;
            _config = config;
            _service = service;
            Initialization = _initializeAsync();
        }

        private async Task _initializeAsync()
        {
            tokens = await CreateToken();
        }

        public async Task<Token> CreateToken(string username = null, string password = null, string role = "User")
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                username = _config.GetValue("Merlot:Username");
                password = _config.GetValue("Merlot:Password");
            }

            var parameters = new Dictionary<string, string>() {
                {"grant_type", "password"},
                {"username", username},
                {"password", password},
                {"role", role}
            };

            var response = await _service.ApiCall("/auth/token", "POST", body: parameters);

            return new Token(await response.Content.ReadAsStringAsync());
            // return JsonConvert.DeserializeObject<Token>(await response.Content.ReadAsStringAsync());
        }

        public async Task<Token> RefreshToken(Token token, string role = "User")
        {
            var parameters = new Dictionary<string, string>() {
                { "grant_type", "refresh_token" },
                { "refresh_token", token.Refresh },
                { "role", role }
            };

            var response = await _service.ApiCall("/auth/token", "POST", body: parameters);

            return new Token(await response.Content.ReadAsStringAsync());
            // return JsonConvert.DeserializeObject<Token>(await response.Content.ReadAsStringAsync());
        }
    }
}
