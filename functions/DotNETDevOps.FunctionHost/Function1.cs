using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using DotNETDevOps.Extensions.AzureFunctions;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using DotNetDevOps.Web;
using Microsoft.Extensions.DependencyInjection;

[assembly: WebJobsStartup(typeof(AspNetCoreWebHostStartUp))]
[assembly: WebJobsStartup(typeof(DotNETDevOps.FunctionHost.WebJobsStartup))]


namespace DotNETDevOps.FunctionHost
{
    public class WebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.Services.AddSingleton<WebHostBuilderConfigurationBuilderExtension>();

        }
    }
    public class WebHostBuilderConfigurationBuilderExtension : IWebHostBuilderExtension
    {
        private readonly Microsoft.Extensions.Hosting.IHostingEnvironment hostingEnvironment;

        public WebHostBuilderConfigurationBuilderExtension(Microsoft.Extensions.Hosting.IHostingEnvironment hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;
        }


        public void ConfigureAppConfiguration(WebHostBuilderContext context, IConfigurationBuilder builder)
        {
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                .AddUserSecrets("93CD8C24-88BA-4141-9E65-7E78FBDB6D95");
        }

        public void ConfigureWebHostBuilder(WebHostBuilder builder)
        {

            builder.ConfigureAppConfiguration(ConfigureAppConfiguration);
            builder.ConfigureLogging(Logging);

            if (hostingEnvironment.IsDevelopment())
            {
                builder.UseContentRoot(Path.Combine(Directory.GetCurrentDirectory(), "../../../../../src/DotNetDevOps.Web"));
            }
            // builder.UseContentRoot();
            //   builder.UseContentRoot(Directory.GetCurrentDirectory());
            // builder.UseContentRoot();
        }

        private void Logging(ILoggingBuilder b)
        {
            //b.AddProvider(new SerilogLoggerProvider(
            //            new LoggerConfiguration()
            //               .MinimumLevel.Verbose()
            //               .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
            //               .Enrich.FromLogContext()
            //                .WriteTo.File($"apptrace.log", buffered: true, flushToDiskInterval: TimeSpan.FromSeconds(30), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1024 * 1024 * 32, rollingInterval: RollingInterval.Hour)
            //               .CreateLogger()));
        }
    }



    [WebHostBuilder(typeof(WebHostBuilderConfigurationBuilderExtension))]
    public class ServerlessApiFunction
    {


        private readonly IAspNetCoreRunner<ServerlessApiFunction> aspNetCoreRunner;

        public ServerlessApiFunction(IAspNetCoreRunner<ServerlessApiFunction> aspNetCoreRunner)
        {
            this.aspNetCoreRunner = aspNetCoreRunner;
        }

        [FunctionName("AspNetCoreHost")]
        public Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "{*all}")]HttpRequest req, ExecutionContext executionContext, ILogger log)
       => aspNetCoreRunner.RunAsync<Startup>(req, executionContext);
    }
}
