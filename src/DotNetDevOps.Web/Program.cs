using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore;
using SInnovations.Unity.AspNetCore;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Extensions;
using System.Net.Http;
using Serilog;
using Unity;
using Microsoft.Extensions.DependencyInjection;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model;
using SInnovations.ServiceFabric.Gateway.Model;
using System.Threading;
using Serilog.Events;
using Unity.Microsoft.DependencyInjection;

namespace DotNetDevOps.Web
{
    public class BrandingOptions
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public string Url { get; set; }
    }
    public class EndpointOptions
    {
        public string ResourceApiEndpoint { get; set; }
    }

    public class Program
    {
        private const string LiterateLogTemplate = "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}";


        public static void Main(string[] args)
        {


            using (var container = new FabricContainer())
            {

                var config = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                 .AddJsonFile($"appsettings.{container.Resolve<IHostingEnvironment>().EnvironmentName}.json", optional: true)
                 .AddEnvironmentVariables();

                container.AddOptions()
                       .UseConfiguration(config) //willl also be set on hostbuilder                      
                       .ConfigureSerilogging(logConfiguration =>
                           logConfiguration.MinimumLevel.Information()
                           .Enrich.FromLogContext()
                           .WriteTo.LiterateConsole(outputTemplate: LiterateLogTemplate))
                       .ConfigureApplicationInsights();

                container.Configure<BrandingOptions>("Branding");
                container.Configure<EndpointOptions>("Endpoints");

                container.RegisterInstance(new HttpClient());

                if (args.Contains("--serviceFabric"))
                {
                    RunInServiceFabric(container);
                }
                else
                {
                    RunOnIIS(container);
                }
            }
        }

        private static void RunOnIIS(IUnityContainer container)
        {
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .CreateLogger();

            var host = new WebHostBuilder()
                 .UseKestrel()
                 .UseContentRoot(Directory.GetCurrentDirectory())
                .UseWebRoot("artifacts/app")
                 .ConfigureLogging(logbuilder =>
                 {

                     logbuilder.AddSerilog();
                 })
                 .UseIISIntegration()
                 .UseStartup<Startup>()
                 .UseApplicationInsights()
                 .UseUnityServiceProvider(container)
                 .Build();

            host.Run();
        }

        private static void RunInServiceFabric(IUnityContainer container)
        {

            container.WithKestrelHosting<Startup>("DotNETDevOps.Web.ServiceType",
                new KestrelHostingServiceOptions
                {
                    GatewayOptions = new GatewayOptions
                    {
                        Key = "DotNETDevOps.Web.ServiceType",
                        ServerName = "www.dotnetdevops.org",
                        ReverseProxyLocation = "/",
                        Ssl = new SslOptions
                        {
                            Enabled = true,
                            SignerEmail = "info@earthml.com"
                        },
                        Properties = new Dictionary<string, object> { { "CloudFlareZoneId", "93ff89ba4caa7ea02c70d27ca9fd9e2e" }, { "www301", true }, { "cf-real-ip", true } },
                    },
                });

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
