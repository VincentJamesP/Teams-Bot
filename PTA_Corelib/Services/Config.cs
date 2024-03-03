using System;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

using KTI.PAL.Teams.Core.Models;
using System.Threading.Tasks;

namespace KTI.PAL.Teams.Core.Services
{
    /// <summary>
    /// Helper class for reading App Configuration.
    /// </summary>
    public sealed class Config
    {
        private readonly IConfiguration _config;
        private readonly IConfigurationRefresher _refresher;
        private readonly ILogger<Config> _logger;

        public Config(ILogger<Config> logger, IConfiguration config, IConfigurationRefresherProvider provider)
        {
            _logger = logger;
            _config = config;
            _refresher = provider.Refreshers.First();
        }

        public string GetValue(string key)
        {
            Task.WaitAll(_refresher.TryRefreshAsync());

            return _config[key];
        }
    }
}
