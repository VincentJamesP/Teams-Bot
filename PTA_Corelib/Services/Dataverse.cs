using System;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace KTI.PAL.Teams.Core.Services
{
    public interface IDataverseService
    {
        /// <summary>
        /// Get the Dataverse ServiceClient with the specified configuration from App Configuration.
        /// </summary>
        /// <returns>A Dataverse ServiceClient.</returns>
        ServiceClient GetClient();
    }

    public sealed class DataverseService : IDataverseService
    {
        private ServiceClient _dataverse;
        private readonly ILogger<DataverseService> _logger;


        public DataverseService() { }
        public DataverseService(ILogger<DataverseService> logger, Config config)
        {
            _logger = logger;

            var connectionString = @$"Url=https://{config.GetValue("Dataverse:Environment")}.dynamics.com;AuthType=ClientSecret;ClientId={config.GetValue("Azure:AppId")};ClientSecret={config.GetValue("Azure:AppSecret")};RequireNewInstance=true";

            _dataverse = new ServiceClient(connectionString, _logger);
        }

        public ServiceClient GetClient()
        {
            return _dataverse;
        }
    }
}
