using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNetDevOps.Web
{
    public static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PostAsJsonAsync<T>(
            this HttpClient httpClient, string url, T data)
        {
            var dataAsString = JsonConvert.SerializeObject(data);
            var content = new StringContent(dataAsString);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return httpClient.PostAsync(url, content);
        }

        public static async Task<T> ReadAsJsonAsync<T>(this HttpContent content)
        {
            var dataAsString = await content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(dataAsString);
        }
    }

    public class DotNetDevOpsResourceProviderController : Controller
    {
        private static async Task<JToken> LoadTemplateAsync(EndpointOptions endpoint, string key)
        {
            var template = await new StreamReader(typeof(DotNetDevOpsResourceProviderController).Assembly.GetManifestResourceStream($"DotNetDevOps.Templates.{key}")).ReadToEndAsync();
            var dict = new ConcurrentDictionary<string, Guid>();
            return JToken.Parse(Regex.Replace(template.Replace("{{HOST}}", endpoint.ResourceApiEndpoint), "{{GUID-.*}}", (m) => dict.GetOrAdd(m.Value, Guid.NewGuid()).ToString()));
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/certificates/demo")]
        public async Task<IActionResult> GetCertificateTemplate([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName, string secretName)
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
        public IActionResult GetCertificateParameters([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName, string secretName)
        {
            var password = "";

            var cert = Certificate.CreateSelfSignCertificatePfx($"CN={keyVaultName}", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), password);

            var x509Certificate = new X509Certificate2(cert, password, X509KeyStorageFlags.Exportable);
            Console.WriteLine($"Certificate {x509Certificate.Issuer} created with thumbprint {x509Certificate.Thumbprint}");



            var certBase64 = Convert.ToBase64String(x509Certificate.Export(X509ContentType.Pkcs12));

            return Content(JObject.FromObject(new Dictionary<string, object>
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
            }).ToString(Newtonsoft.Json.Formatting.Indented), "application/json");

        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/vaults/demo")]
        public async Task<IActionResult> GetVaultsTemplate([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.vault.json");

            if (!string.IsNullOrEmpty(keyVaultName))
            {
                template.SelectToken("$.parameters.keyVaultName")["defaultValue"] = keyVaultName;
            }

            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/ServiceBus/namespaces/demo")]
        public async Task<IActionResult> GetServiceBusTempalte([FromServices] IOptions<EndpointOptions> endpoints, int topicSCaleCount = 2, params string[] correlationMappings)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "ServiceBus.bus.json");

            var topicResources = template.SelectToken("$.resources[0].resources") as JArray;
            var topicTemplate = topicResources.First; topicTemplate.Remove();


            foreach (var correlationMapping in correlationMappings)
            {
                var parts = correlationMapping.Split(":");
                if(parts[0] == "topic")
                {
                    var topic = topicTemplate.DeepClone();
                    topic.SelectToken("$.name").Replace(parts[1]);
                    topic.SelectToken("$.resources").Parent.Remove();
                    topicResources.Add(topic);
                }
            }


                for (var j = 0; j < topicSCaleCount; j++)
            {
                var copy = topicTemplate.DeepClone();
                var topicName = $"[concat(parameters('serviceBusTopicName'),'{String.Format("{0:000}", j)}')]";
                copy.SelectToken("$.name").Replace(topicName);

                topicResources.Add(copy);

                var subscriptionResources = copy.SelectToken("$.resources") as JArray;
                var subscriptionTemplate = subscriptionResources.First; subscriptionTemplate.Remove();

                foreach(var correlationMapping in correlationMappings)
                {
                    var parts = correlationMapping.Split(":"); //topic:pipelines-signalr:EarthMLPipelines.Signalr
                    switch (parts.First())
                    {
                        case "topic":

                            var topicSubscription = subscriptionTemplate.DeepClone();
                            topicSubscription.SelectToken("$.name").Replace($"sub2{parts[1]}");
                            topicSubscription.SelectToken("$.dependsOn").Replace(JToken.FromObject(new[] { topicName,parts[1]}));
                            topicSubscription.SelectToken("$.properties.forwardTo").Replace(parts[1]);

                            subscriptionResources.Add(topicSubscription);

                            var rulesResources = topicSubscription.SelectToken("$.resources") as JArray;
                            var ruleTemplate = rulesResources.First; // ruleTemplate.Remove();

                            ruleTemplate.SelectToken("$.name").Replace($"rule4{parts[1]}");
                            ruleTemplate.SelectToken("$.properties.correlationFilter.correlationId").Replace(parts[2]);

                            ruleTemplate.SelectToken("$.dependsOn[0]").Replace($"sub2{parts[1]}");

                            break;

                        case "queue":

                            break;
                    }
                }



            }
            
        

            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/servicefabric/clusters/demo")]
        public async Task<IActionResult> GetClusterTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withDocker, string vmImagePublisher = "MicrosoftWindowsServer", int additionalIpAddresses = 0, string durabilityLevel="Bronze")
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.fabric.json");

            if (withDocker.HasValue && withDocker.Value)
            {
                template.SelectToken("$.variables.withDocker").Replace(true);

                var settings = template.SelectToken("$.resources[?(@.type == 'Microsoft.ServiceFabric/clusters')].properties.fabricSettings") as JArray;

                settings.Add(JToken.FromObject(new
                {
                    name = "Hosting",
                    parameters = new[]{
                        new {
                            name="SkipDockerProcessManagement",
                            value=true
                        }
                    }
                }));
            }

            template.SelectToken("$.parameters.durabilityLevel.defaultValue").Replace(durabilityLevel);
            template.SelectToken("$.parameters.reliabilityLevel.defaultValue").Replace(durabilityLevel);

            switch (durabilityLevel)
            {
                case "Bronze":
                    template.SelectToken("$.parameters.nt0InstanceCount.defaultValue").Replace(3);
                    break;
                case "Silver":
                    template.SelectToken("$.parameters.nt0InstanceCount.defaultValue").Replace(5);
                    break;
                case "Gold":
                    template.SelectToken("$.parameters.nt0InstanceCount.defaultValue").Replace(5);
                    break;

            }


            if (vmImagePublisher == "Canonical")
            {

                template.SelectToken("$.variables.backendNatPort").Replace(22);
                template.SelectToken("$.parameters.vmImagePublisher.defaultValue").Replace(vmImagePublisher);
                template.SelectToken("$.parameters.vmImageOffer.defaultValue").Replace("UbuntuServer");
                template.SelectToken("$.parameters.vmImageSku.defaultValue").Replace("16.04-LTS");

                template.SelectToken("$.resources[?(@.type == 'Microsoft.ServiceFabric/clusters')].properties.vmImage").Replace("Linux");


            }

            if (additionalIpAddresses > 0)
            {
                var resources = template.SelectToken("$.resources") as JArray;
                var lb = resources.SelectToken("$[?(@.type == 'Microsoft.Network/loadBalancers')]");
                var lbDepsn = lb.SelectToken("$.dependsOn") as JArray;
                var frontendIPConfigurations = lb.SelectToken("$.properties.frontendIPConfigurations") as JArray;

                for (var i = 1; i <= additionalIpAddresses; i++)
                {


                    resources.Add(JToken.FromObject(new
                    {
                        apiVersion = "[variables('publicIPApiVersion')]",
                        type = "Microsoft.Network/publicIPAddresses",
                        name = $"[concat(variables('lbIPName'),'-','{i}')]",
                        location = "[variables('computeLocation')]",
                        properties = new
                        {
                            publicIPAllocationMethod = "Dynamic",
                        },
                        tags = new
                        {
                            resourceType = "Service Fabric",
                            clusterName = "[parameters('clusterName')]"
                        }
                    }));



                  




                    lbDepsn.Add($"[concat('Microsoft.Network/publicIPAddresses/',concat(variables('lbIPName'),'-','{i}'))]");


                    frontendIPConfigurations.Add(JToken.FromObject(new
                    {
                        name = "LoadBalancerIPConfig-" + i,
                        properties = new
                        {
                            publicIPAddress = new
                            {
                                id = $"[resourceId('Microsoft.Network/publicIPAddresses',concat(variables('lbIPName'),'-','{i}'))]"
                            }
                        }
                    }));


                    var gatewayhttp = lb.SelectToken("$.properties.loadBalancingRules[?(@.name == 'GatewayHttp')]").DeepClone();
                    gatewayhttp.SelectToken("$.name").Replace($"Gateway{i}Http");
                    gatewayhttp.SelectToken("$.properties.backendPort").Replace(8080 + i * 1000);
                    gatewayhttp.SelectToken("$.properties.frontendIPConfiguration.id").Replace($"[concat(variables('lbID0'),'/frontendIPConfigurations/LoadBalancerIPConfig-{i}')]");
                    gatewayhttp.SelectToken("$.properties.probe.id").Replace($"[concat(variables('lbID0'),'/probes/AppPortProbe-{i}')]");
                    (lb.SelectToken("$.properties.loadBalancingRules") as JArray).Add(gatewayhttp);

                   

                    var gatewayhttps = lb.SelectToken("$.properties.loadBalancingRules[?(@.name == 'GatewayHttps')]").DeepClone();
                    gatewayhttps.SelectToken("$.name").Replace($"Gateway{i}Https");
                    gatewayhttps.SelectToken("$.properties.backendPort").Replace(8443 + i * 1000);
                    gatewayhttps.SelectToken("$.properties.frontendIPConfiguration.id").Replace($"[concat(variables('lbID0'),'/frontendIPConfigurations/LoadBalancerIPConfig-{i}')]");
                    gatewayhttps.SelectToken("$.properties.probe.id").Replace($"[concat(variables('lbID0'),'/probes/AppPortProbe-{i}')]");
                    (lb.SelectToken("$.properties.loadBalancingRules") as JArray).Add(gatewayhttps);


                    var AppPortProbe = lb.SelectToken("$.properties.probes[?(@.name=='AppPortProbe-0')]").DeepClone();
                    AppPortProbe.SelectToken("$.name").Replace($"AppPortProbe-{i}");
                    AppPortProbe.SelectToken("$.properties.port").Replace(8080 + i * 1000);

                    (lb.SelectToken("$.properties.probes") as JArray).Add(AppPortProbe);


                }
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

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/SInnovations.Gateway/EncryptParamter/demo")]
        public async Task<IActionResult> EncryptParamterForGateway([FromServices] IOptions<EndpointOptions> endpoints, string gatewayUrl, string value, string token)
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "SInnovations.Gateway.EncryptParameter.json");
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var encryptet =await client.PostAsJsonAsync($"{gatewayUrl}/providers/ServiceFabricGateway.Fabric/security/encryptParameter", new
            {
                value = value
            });
            var json = JToken.Parse(await encryptet.Content.ReadAsStringAsync());
            var encryptedBytes = Convert.FromBase64String(json.SelectToken("$.value").ToString());
            var envelope = new EnvelopedCms();
            envelope.Decode(encryptedBytes);

           var RecipientInfo= envelope.RecipientInfos.Cast<RecipientInfo>().First();
            json["recipient"] = JToken.FromObject( RecipientInfo.RecipientIdentifier.Value);
            template.SelectToken("$.outputs.secret.value").Replace(json);

            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/SInnovations.Gateway/Applications")]
        public async Task<IActionResult> GetGatewayApplications([FromServices] IOptions<EndpointOptions> endpoints, string gatewayUrl, string token)
        {
            JToken template = await LoadTemplateAsync(endpoints.Value, "SInnovations.Gateway.Applications.json");


            var url = $"{gatewayUrl}/providers/ServiceFabricGateway.Fabric/applications";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);


            var applications = JArray.Parse(await client.GetStringAsync(url));
            
            foreach(var obj in applications)
            {
                template.SelectToken("$.outputs")[$"{obj["applicationName"].ToString().Replace("fabric:/","")}{obj["applicationTypeName"]}{obj["applicationTypeVersion"]}"] = JToken.FromObject(new { type = "bool", value = true });

            }


            return Ok(template);
        }
        private static ConcurrentDictionary<string, DeploymentModel> _cache = new ConcurrentDictionary<string, DeploymentModel>();

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/servicefabric/applications/GenericApplication")]
        public async Task<IActionResult> GetGenericAppTemplate([FromServices] IOptions<EndpointOptions> endpoints, string packageUrl, string gatewayUrl, string token)
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.GenericApplication.json");





            // var deploymentModel = new DeploymentModel();

            var blob = new CloudBlockBlob(new Uri(packageUrl));
            await blob.FetchAttributesAsync();

            var deploymentModel = _cache.GetOrAdd(blob.Name + blob.Properties.ETag, (key) =>
               {

                   return Task.Run(async () =>
                   {
                       using (var stream = await new HttpClient().GetStreamAsync(packageUrl))
                       {
                           var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                           var entry = zip.GetEntry("ApplicationManifest.xml");

                           XDocument xDocument = await XDocument.LoadAsync(entry.Open(), LoadOptions.None, HttpContext.RequestAborted);
                           var deploymentModel1 = new DeploymentModel();
                           deploymentModel1.ApplicationTypeName = xDocument.Root.Attribute("ApplicationTypeName").Value;
                           deploymentModel1.ApplicationTypeVersion = xDocument.Root.Attribute("ApplicationTypeVersion").Value;
                           return deploymentModel1;
                           // logger.LogInformation("Updated deployment model {@deploymentModel}", deploymentModel);
                       }
                   }).GetAwaiter().GetResult();
               });
          

            template.SelectToken("$.parameters.appPackageUrl.defaultValue").Replace(packageUrl);
            template.SelectToken("$.parameters.applicationTypeName.defaultValue").Replace(deploymentModel.ApplicationTypeName);
            template.SelectToken("$.parameters.applicationTypeVersion.defaultValue").Replace(deploymentModel.ApplicationTypeVersion);

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
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/vmss/scripts/dotnetcore.ps1")]
        public async Task<IActionResult> GetDotNetCoreInstallScript([FromServices] IOptions<EndpointOptions> endpoints)
        {
            var template = await new StreamReader(typeof(Startup).Assembly.GetManifestResourceStream("DotNetDevOps.Templates.VMSS.dotnetcore.ps1")).ReadToEndAsync();
            return Content(template, "text/plain");
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/vmss/scripts/nginx_install.sh")]
        public async Task<IActionResult> GetNginxInstall([FromServices] IOptions<EndpointOptions> endpoints)
        {
            var template = await new StreamReader(typeof(Startup).Assembly.GetManifestResourceStream("DotNetDevOps.Templates.VMSS.nginx_install.sh")).ReadToEndAsync();
            return Content(template, "text/plain");
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/vmss/scalesets/demo")]
        public async Task<IActionResult> GetVmssTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withdocker, bool? withExtensions, bool withAutoscale=false, string vmImagePublisher = "MicrosoftWindowsServer")
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "VMSS.vmss.json");


            if(vmImagePublisher == "Canonical")
            {
                template.SelectToken("$.resources[0].properties.virtualMachineProfile.osProfile.secrets[0].vaultCertificates[0].certificateStore").Parent.Remove();

            }


            var withOutdocker = !withdocker.HasValue || !withdocker.Value;

            if (withOutdocker)
            {
                template.SelectToken("$.resources[0].properties.virtualMachineProfile.extensionProfile.extensions[?(@.name == 'customScript_installdocker')]").Remove();
            }

            foreach (var ext in template.SelectToken("$.resources[0].properties.virtualMachineProfile.extensionProfile.extensions").ToArray())
            {
                if (vmImagePublisher == "Canonical" && ext.SelectToken("osType")?.ToString() == "windows")
                {
                    ext.Remove();
                }
                else if (vmImagePublisher == "MicrosoftWindowsServer" && ext.SelectToken("osType")?.ToString() == "unix")
                {
                    ext.Remove();

                }
                else
                {
                    ext.SelectToken("osType")?.Parent.Remove();
                }


            }

            var withExtensions2 = withExtensions.HasValue && withExtensions.Value;

            foreach (var ext in template.SelectToken("$.resources[0].properties.virtualMachineProfile.extensionProfile.extensions").ToArray())
            {
                

                if (!withExtensions2 && (!(ext.SelectToken("initial")?.ToObject<bool>() ?? false)))
                {
                    ext.Remove();


                }
                
                ext.SelectToken("initial")?.Parent.Remove();

              //  JArray copy = template.SelectToken("$.resources[1].properties.template.resources[0].properties.virtualMachineProfile.extensionProfile.extensions") as JArray;
               // copy.Add(ext.DeepClone());
            }
            template.SelectToken("$.resources[1]").Remove();

            if (!withAutoscale)
            {
                template.SelectToken("$.resources[1]").Remove();
            }





            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/deploy/{*path}")]
         public IActionResult DeployRedirect([FromServices] IOptions<EndpointOptions> endpoints)
        {
            var url = $"{endpoints.Value.ResourceApiEndpoint}{Request.Path}{Request.QueryString}".Replace("DotNetDevOps.AzureTemplates/deploy/", "DotNetDevOps.AzureTemplates/templates/");


            return Redirect(
                $"https://portal.azure.com/#create/Microsoft.Template/uri/{WebUtility.UrlEncode(url)}");

        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/applications/{name}")]
        public async Task<IActionResult> GetEarthMLPipeliens([FromServices] IOptions<EndpointOptions> endpoints, string name)
        {
            var template = await LoadTemplateAsync(endpoints.Value, $"Applications.{name.Replace('-','.')}.json");

            var parameters = template.SelectToken("$.parameters") as JObject;
            foreach(var prop in parameters.Properties())
            {
                if(Request.Query.ContainsKey(prop.Name))
                {
                    var param = prop.Value;
                    param["defaultValue"] = Request.Query[prop.Name].FirstOrDefault();
                }
            }

            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/demo")]
        public async Task<IActionResult> GetDemoTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withApp, bool? withDocker, string vmImagePublisher = "MicrosoftWindowsServer", string gatewayVersionPrefix="ci")
        {
            var template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.root.json");

            var withOutApp = !withApp.HasValue || !withApp.Value;
            if (withOutApp)
            {
                template.SelectToken("$.resources[?(@.name == 'DeploySInnovationsGateway')]").Remove();
                template.SelectToken("$.parameters.letsencryptSignerEmail").Parent.Remove();
                template.SelectToken("$.parameters.gatewayVersion").Parent.Remove();
                template.SelectToken("$.parameters.explorerVersion").Parent.Remove();
            }
            else
            {
                {
                    var type = "S-Innovations.ServiceFabric.GatewayApplicationType/";
                    var container = new CloudBlobContainer(new Uri("https://cdn.dotnetdevops.org/sfapps"));
                    var blobs = await container.ListBlobsSegmentedAsync(type, true, BlobListingDetails.None, null, null, null, null);
                    var folders = blobs.Results.OfType<CloudBlockBlob>()
                        .GroupBy(b => b.Parent.Prefix.Substring(type.Length).Trim('/'))
                        .Select(c => c.Key + "/" + Path.GetFileNameWithoutExtension(c.Where(b => !string.Equals(Path.GetFileNameWithoutExtension(b.Name), "latest", StringComparison.OrdinalIgnoreCase)).Last().Name))
                        .Select(c => c.Trim('/'))
                        .ToArray();


                    template.SelectToken("$.parameters.gatewayVersion.allowedValues").Replace(JArray.FromObject(folders));
                    template.SelectToken("$.parameters.gatewayVersion.defaultValue").Replace(folders.FirstOrDefault(k=>k.StartsWith(gatewayVersionPrefix)));
                 //   template


                }

                {
                    var type = "ServiceFabricGateway.ExplorerApplicationType/";
                    var container = new CloudBlobContainer(new Uri("https://cdn.dotnetdevops.org/sfapps"));
                    var blobs = await container.ListBlobsSegmentedAsync(type, true, BlobListingDetails.None, null, null, null, null);
                    var folders = blobs.Results.OfType<CloudBlockBlob>()
                        .GroupBy(b => b.Parent.Prefix.Substring(type.Length).Trim('/'))
                        .Select(c => c.Key + "/" + Path.GetFileNameWithoutExtension(c.Where(b => !string.Equals(Path.GetFileNameWithoutExtension(b.Name), "latest", StringComparison.OrdinalIgnoreCase)).Last().Name))
                        .Select(c => c.Trim('/'))
                        .ToArray();



                    template.SelectToken("$.parameters.explorerVersion.allowedValues").Replace(JArray.FromObject(folders));
                    template.SelectToken("$.parameters.explorerVersion.defaultValue").Replace(folders.FirstOrDefault(k => k.StartsWith(gatewayVersionPrefix)));

                }
            }

            if (withDocker.HasValue && withDocker.Value)
            {
                template.SelectToken("$.variables.withDocker").Replace(true);
            }

            template.SelectToken("$.parameters.vmImagePublisher.defaultValue").Replace(vmImagePublisher);



           



            return Ok(template);
        }


    }
}
