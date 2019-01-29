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


            var host = new FabricHostBuilder()
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
              })
                .ConfigureSerilogging((context, logConfig) =>
                    logConfig.MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.LiterateConsole(outputTemplate: LiterateLogTemplate)
                )
                .ConfigureApplicationInsights()
                .Configure<BrandingOptions>("Branding")
                .Configure<EndpointOptions>("Endpoints");




            if (args.Contains("--serviceFabric"))
            {
                // config.AddServiceFabricConfig("Config"); // Add Service Fabric configuration settings.
                await RunFabric(host);
            }
            else
            {
                await RunIis(host);
            }
        }

        private static async Task RunIis(IHostBuilder container)
        {
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .CreateLogger();

            var app = container.Build();

            var host = new WebHostBuilder()
              .UseKestrel()
              .ConfigureServices((context, services) =>
              {

                  services.AddSingleton(app.Services.GetService<ILifetimeScope>().BeginLifetimeScope());
                  services.AddSingleton(sp => sp.GetRequiredService<ILifetimeScope>().Resolve<IServiceProviderFactory<IServiceCollection>>());
              })
              .UseContentRoot(Directory.GetCurrentDirectory())
              .UseWebRoot("artifacts/app")
              .ConfigureLogging(logbuilder =>
              {

                  logbuilder.AddSerilog();
              })
              .UseIISIntegration()
              .UseStartup<Startup>()
              .UseApplicationInsights()
              .Build();


            await app.StartAsync();

            await host.RunAsync();

            await app.StopAsync();
        }

        private static async Task RunFabric(IHostBuilder container)
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
                            SignerEmail = "info@earthml.com",
                            UseHttp01Challenge = true
                        },
                        Properties = new Dictionary<string, object> { { "www301", true }, { "cf-real-ip", true } },
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
                            SignerEmail = "info@earthml.com",
                            UseHttp01Challenge = true
                        },
                        Properties = new Dictionary<string, object> {   { "cf-real-ip", true } },
                    },
                    }
                });

            await container.Build().RunAsync();
        }
    }
}
