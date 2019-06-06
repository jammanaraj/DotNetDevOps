using DotNetDevOps.Web;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetDevOps
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var cdn = new CDNHelper("https://dotnetdevops.blob.core.windows.net/functions", "DotNETDevOps.FrontDoor.RouterFunction");
            //var a = cdn.GetAsync().Result;

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
