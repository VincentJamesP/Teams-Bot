using System;
using System.IO;
using System.Threading.Tasks;

using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

using Polly;
using Polly.Extensions.Http;

using KTI.PAL.Teams.Core.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace KTI.PAL.Teams.Functions
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(app =>
                {
                    app.AddEnvironmentVariables();
                    app.AddJsonFile("appsettings.json");

                    IConfigurationRoot config = null;
                    string appConfig = null;
                    var bin = AppDomain.CurrentDomain.BaseDirectory;
                    var currentDirectory = Directory.GetCurrentDirectory();

                    // use appsettings.json if it contains CoreConfig
                    if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")))
                    {
                        config = new ConfigurationBuilder()
                            .AddJsonFile(Path.Combine(currentDirectory, "appsettings.json"), optional: false)
                            .Build();
                        appConfig = config.GetValue<string>("ConnectionStrings:AppConfig");
                    }

                    if (appConfig is null)
                        throw new ArgumentNullException(nameof(config), "Cannot get connection strings.");

                    app.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(appConfig)
                        .Select("*")
                        .ConfigureRefresh(refresh => refresh.Register("App:Updated", refreshAll: true).SetCacheExpiration(new TimeSpan(0, 1, 0)));
                    });
                })
                .ConfigureHostConfiguration(host => { })
                .ConfigureFunctionsWorkerDefaults(worker =>
                {
                    worker.Services.Configure<WorkerOptions>(options =>
                    {
                        var settings = NewtonsoftJsonObjectSerializer.CreateJsonSerializerSettings();
                        settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                        settings.NullValueHandling = NullValueHandling.Include;

                        options.Serializer = new NewtonsoftJsonObjectSerializer(settings);
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddAzureAppConfiguration();
                    services.AddLogging(cfg => cfg.AddConsole());
                    services.AddSingleton<Config>();
                    services.AddSingleton<Microsoft.Graph.IAuthenticationProvider, AuthProvider>();
                    services.AddHttpClient<IMerlotService, MerlotService>()
                            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                            .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
                                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                                .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

                    services.AddSingleton<Core.Services.IGraphService, Core.Services.GraphService>();
                    services.AddTransient<Core.Domains.Graph.IGraphTeamDomain, Core.Domains.Graph.Teams>();
                    services.AddTransient<Core.Domains.Graph.IGraphUserDomain, Core.Domains.Graph.Users>();
                    services.AddTransient<Core.Domains.Graph.IGraphCalendarDomain, Core.Domains.Graph.Calendar>();

                    services.AddSingleton<Core.Services.IMerlotService, Core.Services.MerlotService>();
                    services.AddTransient<Core.Domains.Merlot.IMerlotAuthentication, Core.Domains.Merlot.Authentication>();
                    services.AddTransient<Core.Domains.Merlot.IMerlotEmployee, Core.Domains.Merlot.Employees>();
                    services.AddTransient<Core.Domains.Merlot.IMerlotFlight, Core.Domains.Merlot.Flights>();
                    services.AddTransient<Core.Domains.Merlot.IMerlotPairing, Core.Domains.Merlot.Pairings>();

                    services.AddSingleton<Core.Services.IDataverseService, Core.Services.DataverseService>();
                    services.AddTransient<Core.Domains.Dataverse.IDataverseFlight, Core.Domains.Dataverse.Flights>();
                    services.AddTransient<Core.Domains.Dataverse.IDataverseCrew, Core.Domains.Dataverse.Crew>();
                    services.AddTransient<Core.Domains.Dataverse.IDataverseDuty, Core.Domains.Dataverse.Duty>();

                    services.AddTransient<Core.Domains.PAL.IPAL, Core.Domains.PAL.PAL>();

                    services.Configure<LoggerFilterOptions>(cfg => cfg.MinLevel = LogLevel.Information);
                })
                .Build();

            host.Run();
        }
    }
}
