using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Graph;

using KTI.PAL.Teams.Core.Models.Graph;
using System.Net.Http;

namespace KTI.PAL.Teams.Core.Services
{
    public interface IGraphService
    {
        /// <summary>
        /// Get the Graph ServiceClient with the specified configuration from App Configuration.
        /// </summary>
        /// <returns>A Graph ServiceClient.</returns>
        GraphServiceClient GetClient();
    }

    public sealed class GraphService : IGraphService
    {
        private string[] _scope;
        private string _tenantId;
        private string _appId;

        private GraphServiceClient _graph;
        private static readonly HttpProvider _httpProvider = new HttpProvider(new HttpClientHandler(), false);
        private readonly ILogger<GraphService> _logger;
        private readonly Config _config;

        public GraphService(IAuthenticationProvider authProvider, ILogger<GraphService> logger, Config config)
        {
            _logger = logger;
            _config = config;
            _scope = _config.GetValue("Graph:Scope").Split(',');
            _tenantId = _config.GetValue("Azure:TenantId");
            _appId = _config.GetValue("Azure:AppId");

            _graph = new GraphServiceClient(authProvider, _httpProvider);
        }

        public GraphServiceClient GetClient()
        {
            return _graph;
        }
    }

    public class AuthProvider : IAuthenticationProvider
    {
        private readonly ILogger<AuthProvider> _logger;
        private readonly Config _config;
        private Token _token = null;

        public AuthProvider(ILogger<AuthProvider> logger, Config config)
        {
            _logger = logger;
            _config = config;
        }

        public AuthProvider(Token token)
        {
            _token = token;
        }

        private Token GetToken(string appId = null, string tenantId = null, string secret = null)
        {
            // use token if still valid
            if (_token is not null && _token.ExpiresOn > DateTimeOffset.UtcNow)
                return _token;

            // otherwise, request for a new token
            var app = ConfidentialClientApplicationBuilder.Create(appId ?? _config.GetValue("Azure:AppId"))
                .WithTenantId(tenantId ?? _config.GetValue("Azure:TenantId"))
                .WithClientSecret(secret ?? _config.GetValue("Azure:AppSecret"))
                .Build();

            AuthenticationResult result = null;

            try
            {
                result = app.AcquireTokenForClient(_config.GetValue("Graph:Scope").Split(',')).ExecuteAsync().Result;
            }
            catch (MsalServiceException e) when (e.Message.Contains("AADSTS70011"))
            {
                _logger.LogError(e, e.Message);
                throw e;
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                throw e;
            }

            _token = new()
            {
                TokenType = result.TokenType,
                ExpiresOn = result.ExpiresOn,
                AccessToken = result.AccessToken
            };

            return _token;
        }

        public Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var token = this.GetToken();
            request.Headers.Add("Authorization", $"Bearer {token.AccessToken}");
            return Task.CompletedTask;
        }
    }
}
