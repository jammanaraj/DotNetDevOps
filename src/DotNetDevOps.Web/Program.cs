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
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Extensions;
using System.Net.Http;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model;
using SInnovations.ServiceFabric.Gateway.Model;
using System.Threading;
using Serilog.Events;
using DotNetDevOps.ServiceFabric.Hosting;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Configuration;
using Microsoft.Extensions.Hosting;
using Autofac;

namespace DotNetDevOps.Web
{

    public class Program
    {
        private const string LiterateLogTemplate = "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}";
        /// <summary>
        /// Event Handler delegate to log if an unhandled AppDomain exception occurs.
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the exception details</param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            //  ServiceEventSource.Current.UnhandledException(ex.GetType().Name, ex.Message, ex.StackTrace);
        }

        /// <summary>
        /// Event Handler delegate to log if an unobserved task exception occurs.
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the exception details</param>
        /// <remarks>
        /// We intentionally do not mark the exception as Observed, which would prevent the process from being terminated.
        /// We want the unobserved exception to take out the process. Note, as of .NET 4.5 this relies on the ThrowUnobservedTaskExceptions
        /// runtime configuration in the host App.Config settings.
        /// </remarks>
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            //  ServiceEventSource.Current.UnobservedTaskException(e.Exception?.GetType().Name, e.Exception?.Message, e.Exception?.StackTrace);

            AggregateException flattened = e.Exception?.Flatten();
            foreach (Exception ex in flattened?.InnerExceptions)
            {
                //   ServiceEventSource.Current.UnobservedTaskException(ex.GetType().Name, ex.Message, ex.StackTrace);
            }

            // Marking as observed to prevent process exit.
            // e.SetObserved();
        }

        public static async Task Main(string[] args)
        {

            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", null);
            // var cp = CertificateProvider.GetProvider("BouncyCastle");

            // Setup unhandled exception handlers.
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;


            var builder = new FabricHostBuilder(args)
              //Add fabric configuration provider
              .ConfigureAppConfiguration((context, configurationBuilder) =>
              {
                  configurationBuilder
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                   .AddEnvironmentVariables();

                  if (args.Contains("--serviceFabric"))
                  {
                      configurationBuilder.AddServiceFabricConfig("Config");
                  }
              }).ConfigureServices((context, services) =>
              {

                  services.WithKestrelHosting<Startup>(Constants.DotNETDevOpsServiceType, Factory);


              })
                .ConfigureSerilogging((context, logConfig) =>
                    logConfig.MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.File("trace.log", retainedFileCountLimit: 5, fileSizeLimitBytes: 1024 * 1024 * 10)
                    .WriteTo.LiterateConsole(outputTemplate: LiterateLogTemplate)
                )
                .ConfigureApplicationInsights()
                .Configure<BrandingOptions>("Branding")
                .Configure<EndpointOptions>("Endpoints");


            await builder.RunConsoleAsync();

           
        }

        private static KestrelHostingServiceOptions Factory(IComponentContext arg)
        {
            return new KestrelHostingServiceOptions
            {
                GatewayOptions = new GatewayOptions
                {
                    Key = Constants.DotNETDevOpsServiceType,
                    ServerName = "www.dotnetdevops.org",
                    ReverseProxyLocation = "/",
                    Ssl = new SslOptions
                    {
                        Enabled = true,
                        SignerEmail = "info@dotnetdevops.org",
                        UseHttp01Challenge = false
                    },
                    Properties = new Dictionary<string, object> { ["www301"] = true, ["cf-real-ip"] = true, ["CloudFlareZoneId"] = "93ff89ba4caa7ea02c70d27ca9fd9e2e" },
                },
                AdditionalGateways = new[]
                    {
                        new GatewayOptions{
                        Key = "DotNETDevOps.ServiceProvider",
                        ServerName = "management.dotnetdevops.org",
                        ReverseProxyLocation = new string[]{ "DotNetDevOps.AzureTemplates"}.BuildResourceProviderLocation(),
                        Ssl = new SslOptions
                        {
                            Enabled = true,
                            SignerEmail = "info@dotnetdevops.org",
                            UseHttp01Challenge = false
                        },
                        Properties = new Dictionary<string, object> {  ["cf-real-ip"]= true ,["CloudFlareZoneId"]="93ff89ba4caa7ea02c70d27ca9fd9e2e"  },
                    },
                    }
            };

        }
 
    
    }
}
