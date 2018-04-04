using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using Unity;

namespace DotNetDevOps.Web
{
   
    public class DotNetDevOpsResourceProviderController : Controller
    {
        private static async Task<JToken> LoadTemplateAsync(EndpointOptions endpoint, string key)
        {
            var template = await new StreamReader(typeof(DotNetDevOpsResourceProviderController).Assembly.GetManifestResourceStream($"DotNetDevOps.Templates.{key}")).ReadToEndAsync();
            var dict = new ConcurrentDictionary<string, Guid>();
            return JToken.Parse(Regex.Replace(template.Replace("{{HOST}}", endpoint.ResourceApiEndpoint), "{{GUID-.*}}", (m) => dict.GetOrAdd(m.Value, Guid.NewGuid()).ToString()));
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/certificates/demo")]
        public async Task<IActionResult> GetCertificateTemplate([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName,string secretName)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.secret.json");

            
            if (!string.IsNullOrEmpty(keyVaultName))
            {
                template.SelectToken("$.parameters.keyVaultName")["defaultValue"] = keyVaultName;
            }
            if (!string.IsNullOrEmpty(secretName))
            {
                template.SelectToken("$.parameters.secretName")["defaultValue"] = secretName;
            }

           
           
            

            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/certificates/demo/parameters")]
        public async Task<IActionResult> GetCertificateParameters([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName, string secretName)
        {
            var password = "";

            var cert = Certificate.CreateSelfSignCertificatePfx($"CN={keyVaultName}", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), password);

            var x509Certificate = new X509Certificate2(cert, password, X509KeyStorageFlags.Exportable);
            Console.WriteLine($"Certificate {x509Certificate.Issuer} created with thumbprint {x509Certificate.Thumbprint}");

         

            var certBase64 = Convert.ToBase64String(x509Certificate.Export(X509ContentType.Pkcs12));

            return Content(JObject.FromObject(new Dictionary<string,object>
            {
                {"$schema" ,"https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#"},
                {"contentVersion","1.0.0.0" },
                { "parameters", new {
                    keyVaultName=new
                    {
                        value=keyVaultName
                    },
                    secretName=new
                    {
                        value=secretName
                    },
                    secretValue=new
                    {
                        value=certBase64
                    },
                    certificateThumbprint = new
                    {
                        value=x509Certificate.Thumbprint
                    }

                } }
            }).ToString(Newtonsoft.Json.Formatting.Indented),"application/json");
           
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/vaults/demo")]
        public async Task<IActionResult> GetVaultsTemplate([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName)
        {
            var template = await LoadTemplateAsync(endpoints.Value,"KeyVault.vault.json");

            if (!string.IsNullOrEmpty(keyVaultName))
            {
                template.SelectToken("$.parameters.keyVaultName")["defaultValue"] = keyVaultName;
            }

            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/servicefabric/clusters/demo")]
        public async Task<IActionResult> GetClusterTemplate([FromServices] IOptions<EndpointOptions> endpoints, string vmImagePublisher = "MicrosoftWindowsServer")
        {

            JToken template = await LoadTemplateAsync(endpoints.Value,"ServiceFabric.fabric.json");

            if(vmImagePublisher == "Canonical")
            {

                template.SelectToken("$.variables.backendNatPort").Replace(22);
                template.SelectToken("$.parameters.vmImagePublisher.defaultValue").Replace(vmImagePublisher);
                template.SelectToken("$.parameters.vmImageOffer.defaultValue").Replace("UbuntuServer");
                template.SelectToken("$.parameters.vmImageSku.defaultValue").Replace("16.04-LTS");

                template.SelectToken("$.resources[?(@.type == 'Microsoft.ServiceFabric/clusters')].properties.vmImage").Replace("Linux");
            }


            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVaults/accessPolicies/demo")]
        public async Task<IActionResult> GetAccessPolicyTemplate([FromServices] IOptions<EndpointOptions> endpoints)
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "KeyVault.msi.json");
            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/servicefabric/apps/demo")]
        public async Task<IActionResult> GetAppTemplate([FromServices] IOptions<EndpointOptions> endpoints)
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.app.json");
            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/accessPolicies/demo")]
        public async Task<IActionResult> GetAccessPolictyTemplate([FromServices] IOptions<EndpointOptions> endpoints)
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.app.json");
            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/vmss/scripts/docker_install.ps1")]
        public async Task<IActionResult> GetVMSSScript([FromServices] IOptions<EndpointOptions> endpoints)
        {
            var template = await new StreamReader(typeof(Startup).Assembly.GetManifestResourceStream("DotNetDevOps.Templates.VMSS.docker_install.ps1")).ReadToEndAsync();
            return Content(template, "text/plain");
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/vmss/scripts/nginx_install.sh")]
        public async Task<IActionResult> GetNginxInstall([FromServices] IOptions<EndpointOptions> endpoints)
        {
            var template = await new StreamReader(typeof(Startup).Assembly.GetManifestResourceStream("DotNetDevOps.Templates.VMSS.nginx_install.sh")).ReadToEndAsync();
            return Content(template, "text/plain");
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/vmss/scalesets/demo")]
        public async Task<IActionResult> GetVmssTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withdocker, bool? withExtensions, string vmImagePublisher = "MicrosoftWindowsServer")
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "VMSS.vmss.json");
              

            var withOutdocker = !withdocker.HasValue || !withdocker.Value;
            if (withOutdocker)
            {
                template.SelectToken("$.resources[0].properties.virtualMachineProfile.extensionProfile.extensions[?(@.name == 'customScript')]").Remove();
            }

            var withExtensions2 =  withExtensions.HasValue &&  withExtensions.Value;

            foreach (var ext in template.SelectToken("$.resources[0].properties.virtualMachineProfile.extensionProfile.extensions").ToArray())
            {
                if (!withExtensions2 && (!(ext.SelectToken("initial")?.ToObject<bool>() ?? false)))
                {
                    ext.Remove();
                }
                else
                {
                    ext.SelectToken("initial")?.Parent.Remove();
                }
            }

            foreach (var ext in template.SelectToken("$.resources[0].properties.virtualMachineProfile.extensionProfile.extensions").ToArray())
            {
                if (vmImagePublisher == "Canonical" && ext.SelectToken("osType")?.ToString() == "windows"  )
                {
                    ext.Remove();
                }
                else if (vmImagePublisher == "MicrosoftWindowsServer" && ext.SelectToken("osType")?.ToString() == "unix")
                {
                    ext.Remove();

                } else
                {
                    ext.SelectToken("osType")?.Parent.Remove();
                }

               
            }



                return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/demo")]
        public async Task<IActionResult> GetDemoTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withApp, string vmImagePublisher = "MicrosoftWindowsServer")
        {
            var template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.root.json");

            var withOutApp = !withApp.HasValue || !withApp.Value;
            if (withOutApp)
            {
                template.SelectToken("$.resources[?(@.name == 'DeploySInnovationsGateway')]").Remove();
            }

            template.SelectToken("$.parameters.vmImagePublisher.defaultValue").Replace(vmImagePublisher);

            {
                var container = new CloudBlobContainer(new Uri("https://cdn.earthml.com/sfapps"));
                var blobs = await container.ListBlobsSegmentedAsync("S-Innovations.ServiceFabric.GatewayApplicationType/CI/", null);
                var folders = blobs.Results.OfType<CloudBlockBlob>().Where(b=> !string.Equals( Path.GetFileNameWithoutExtension( b.Name) , "latest",StringComparison.OrdinalIgnoreCase));
                var version = Path.GetFileNameWithoutExtension(folders.Last().Name);
                template.SelectToken("$.parameters.gatewayVersion.defaultValue").Replace(version);
             
            }
            {
                var container = new CloudBlobContainer(new Uri("https://cdn.earthml.com/sfapps"));
                var blobs = await container.ListBlobsSegmentedAsync("ServiceFabricGateway.ExplorerApplicationType/CI/", null);
                var folders = blobs.Results.OfType<CloudBlockBlob>().Where(b => !string.Equals(Path.GetFileNameWithoutExtension(b.Name), "latest", StringComparison.OrdinalIgnoreCase)); ;
                var version = Path.GetFileNameWithoutExtension(folders.Last().Name);
                template.SelectToken("$.parameters.explorerVersion.defaultValue").Replace(version);
                
            }

            return Ok(template);
        }


    }
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddMvc();


            services.AddHsts(o => { o.IncludeSubDomains = false; o.Preload = true; });
            services.AddHttpsRedirection(o => { });

        }
        public void ConfigureContainer(IUnityContainer container)
        {
            container.RegisterInstance("This string is displayed if container configured correctly",
                                       "This string is displayed if container configured correctly");


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
            //    app.UseDeveloperExceptionPage();
            }
           // app.UseHttpsRedirection();
            //app.UseHsts();

            app.UseStaticFiles();

            app.UseMvc();
        }
    }
}
