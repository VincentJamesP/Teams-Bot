using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

using KTI.PAL.Teams.Core.Models.Merlot;
using System.Threading;

namespace KTI.PAL.Teams.Core.Services
{
    public interface IMerlotService
    {
        /// <summary>
        /// Make an unauthenticated API call to Merlot.
        /// </summary>
        /// <param name="endpoint">The api endpoint.</param>
        /// <param name="method">The HttpMethod to use.</param>
        /// <param name="body">The contents to put in the request body, if there are any.</param>
        /// <param name="query">The queries to add to the url, if there are any.</param>
        /// <returns>The response from Merlot.</returns>
        Task<HttpResponseMessage> ApiCall(string endpoint, string method, Dictionary<string, string> body = null, Dictionary<string, string> query = null);
        /// <summary>
        /// Make an authenticated API call to Merlot. Handles token refreshing before adding it as a header Authentication value.
        /// </summary>
        /// <param name="endpoint">The api endpoint.</param>
        /// <param name="method">The HttpMethod to use.</param>
        /// <param name="body">The contents to put in the request body, if there are any.</param>
        /// <param name="query">The queries to add to the url, if there are any.</param>
        /// <returns>The response from Merlot.</returns>
        Task<HttpResponseMessage> AuthenticatedApiCall(string endpoint, string method, Dictionary<string, string> body = null, Dictionary<string, string> query = null);
    }

    public sealed class MerlotService : IMerlotService
    {
        private readonly Config _config;
        private static SemaphoreSlim _refreshLock = new(1, 1);
        private Core.Models.Merlot.Token _tokens { get => _auth.tokens; }
        private Core.Domains.Merlot.Authentication _auth { get; set; }
        private readonly string _url;

        private readonly HttpClient _client;
        private ILogger<MerlotService> _logger { get; set; }

        public MerlotService() { }

        public MerlotService(ILogger<MerlotService> logger, ILogger<Core.Domains.Merlot.Authentication> authLogger, HttpClient client, Config config)
        {
            _logger = logger;
            _config = config;

            if (_config.GetValue("App:Environment").Equals("production"))
                _url = _config.GetValue("Merlot:BaseUrlProd");
            else
                _url = _config.GetValue("Merlot:BaseUrlDev");

            _client = client;
            _client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _config.GetValue("Merlot:OcpApimSubscriptionKey"));
            _client.DefaultRequestHeaders.Add("Env", _config.GetValue("Merlot:Env"));
            _client.Timeout = new TimeSpan(0, 5, 0);

            _auth = new(authLogger, this, config);
        }

        private async Task<Token> _updateTokens()
        {
            // wait for authentication domain to fetch initial tokens
            await _auth.Initialization;
            await _refreshLock.WaitAsync();

            // are tokens null or expired?
            if ((_tokens?.ExpiresOn - DateTime.UtcNow)?.TotalSeconds <= 0)
            {
                await _auth.CreateToken();
            }
            // are tokens within 2 minutes of expiring?
            else
            {
                var timeLeft = (_tokens.ExpiresOn - DateTime.UtcNow).TotalSeconds;
                if (timeLeft < (60 * 2))
                {
                    _logger.LogInformation($"{timeLeft}s left before tokens expire, refreshing tokens");
                    await _auth.RefreshToken(_tokens);
                }
            }

            _refreshLock.Release();
            return _tokens;
        }

        public async Task<HttpResponseMessage> ApiCall(string endpoint, string method, Dictionary<string, string> body = null, Dictionary<string, string> query = null)
        {
            string url = $"{_url}{endpoint}";

            if (query is not null)
                url = QueryHelpers.AddQueryString(url, query);


            HttpRequestMessage req = new(new HttpMethod(method), url);

            if (body is not null)
            {
                req.Content = new FormUrlEncodedContent(body);
            }

            var response = await _client.SendAsync(req);
            _logger.LogInformation($"{method} {url}");

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();

                _logger.LogError($"      unsuccessful, response from Merlot: {message}");
                throw new MerlotException(message);
            }

            return response;
        }

        public async Task<HttpResponseMessage> AuthenticatedApiCall(string endpoint, string method, Dictionary<string, string> body = null, Dictionary<string, string> query = null)
        {
            _auth.tokens = await _updateTokens();

            string url = $"{_url}{endpoint}";

            if (query is not null)
                url = QueryHelpers.AddQueryString(url, query);


            HttpRequestMessage req = new(new HttpMethod(method), url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens.Access);

            if (body is not null)
            {
                // req.Content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                req.Content = new FormUrlEncodedContent(body);
            }

            HttpResponseMessage response = null;

            try
            {
                response = await _client.SendAsync(req);
                _logger.LogInformation($"{method} {endpoint}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"FAILED {method} {endpoint}: {e.Message}");
            }


            if (response is not null && !response.IsSuccessStatusCode)
            {
                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

                if (json.TryGetProperty("error", out var err))
                {
                    string description = null;
                    if (json.TryGetProperty("error_description", out var jsonDesc))
                        description = jsonDesc.GetString();

                    _logger.LogError($"{response.StatusCode.ToString()}: {err.GetString()} (details: {description})");
                    throw new MerlotException(err.GetString());
                }
                else if (json.TryGetProperty("message", out var msg))
                {
                    _logger.LogWarning($"{response.StatusCode.ToString()}: {msg.GetString()}");

                    if (msg.GetString().Equals("Could not process the request due to the input data is invalid."))
                    {
                        return null;
                    }
                }
            }

            return response;
        }
    }
}
