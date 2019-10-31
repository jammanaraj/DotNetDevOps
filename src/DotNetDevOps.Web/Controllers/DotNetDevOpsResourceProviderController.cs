using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SemanticVersions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

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

    public class LibVersion
    {
        public string Version { get; set; }
    }
    public class CDNHelper
    {
        public string url { get; }
        public string lib { get; }

        public CDNHelper(string url, string lib)
        {
            this.url = url;
            this.lib = lib;
        }
        private static char[] splits = new[] { '/' };

        public async Task<LibVersion> GetAsync(string filter = "*")
        {
            var blob = new CloudBlobContainer(new Uri(url));
            if (!await blob.ExistsAsync())
            {
                return null;
            }
            var versions = await blob.ListBlobsSegmentedAsync($"{lib}/", null);

            var sems = versions.Results.OfType<CloudBlobDirectory>()
                .Where(c => SemanticVersion.TryParse(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last(), out SemanticVersion semver) && semver.Satisfies(filter))
                .OrderByDescending(c => new SemanticVersion(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last()))
                .ToArray();
            if (!sems.Any())
            {
                return null;
            }

            return new LibVersion { Version = sems.FirstOrDefault().Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last() };

        }
    }

    public class DotNetDevOpsResourceProviderController : Controller
    {
        private static async Task<JToken> LoadTemplateAsync(EndpointOptions endpoint, string key, IQueryCollection query = null)
        {
            var template = await new StreamReader(typeof(DotNetDevOpsResourceProviderController).Assembly.GetManifestResourceStream($"DotNetDevOps.Templates.{key}")).ReadToEndAsync();
            var dict = new ConcurrentDictionary<string, Guid>();
            var tmp = JToken.Parse(Regex.Replace(template.Replace("{{HOST}}", endpoint.ResourceApiEndpoint), "{{GUID-.*}}", (m) => dict.GetOrAdd(m.Value, Guid.NewGuid()).ToString()));

            if (query != null)
            {
                var parameters = tmp.SelectToken("$.parameters") as JObject;
                if (parameters != null)
                {
                    foreach (var prop in parameters.Properties())
                    {
                        var reference = prop.Value.SelectToken("$.reference");
                        if (reference != null)
                        {
                            reference.Parent.Remove();
                        }

                        if (query.ContainsKey(prop.Name))
                        {
                            var param = prop.Value;
                            param["defaultValue"] = query[prop.Name].FirstOrDefault(); // reference != null? Encoding.Unicode.GetString( dataProtector.Unprotect(Base64.DecodeToByteArray(Request.Query[prop.Name]))) :  Request.Query[prop.Name].FirstOrDefault();
                        }


                    }
                }
            }
            return tmp;
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

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/azure-function")]
        public async Task<IActionResult> GetAzureFunctionDeployment([FromServices] IOptions<EndpointOptions> endpoints, string function, [FromServices] CloudStorageAccount cloudStorageAccount)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "AzureFunctions.Function.json", Request.Query);

            var functionContainer = cloudStorageAccount.CreateCloudBlobClient().GetContainerReference("functions");

            var cdnHelper = new CDNHelper(functionContainer.Uri.ToString(), function);
            var latest = await cdnHelper.GetAsync();
            var functionBlob = functionContainer.GetBlockBlobReference(function + "/" + latest.Version + "/" + function + ".zip");
            template.SelectToken("$.parameters.artifactsUri")["defaultValue"] = functionBlob.Uri;

            var appsettings = template.SelectToken("$.resources[0].properties.siteConfig.appSettings") as JArray;

            foreach (var query in Request.Query.Where(k => k.Key.StartsWith("appsetting_")))
            {
                appsettings.Add(new JObject(
                    new JProperty("name", query.Key.Substring("appsetting_".Length)),
                    new JProperty("value", query.Value.FirstOrDefault())
                    ));
            }

            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/AddAccessPolicy")]
        public async Task<IActionResult> GetAddAccessPolicyTemplate([FromServices] IOptions<EndpointOptions> endpoints, string function, string containerUri)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.AddAccessPolicy.json", Request.Query);
            return Ok(template);
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/AzureFunctions/WithMSI")]
        public async Task<IActionResult> GetAzureFunctionDeploymentWithMSI([FromServices] IOptions<EndpointOptions> endpoints, string function, string containerUri, string functions_extension_version = "~2")
        {
            var template = await LoadTemplateAsync(endpoints.Value, "AzureFunctions.FunctionWithKeyVault.json", Request.Query);

            if (!string.IsNullOrEmpty(containerUri))
            {
                var functionContainer = new CloudBlobContainer(new Uri(containerUri));

                var cdnHelper = new CDNHelper(functionContainer.Uri.ToString(), function);
                var latest = await cdnHelper.GetAsync();
                var functionBlob = functionContainer.GetBlockBlobReference(function + "/" + latest.Version + "/" + function + ".zip");
                template.SelectToken("$.parameters.artifactsUri")["defaultValue"] = functionBlob.Uri;
            }

            var appsettings = template.SelectToken("$.variables.localAppSettings") as JArray;
            appsettings.SelectToken("$[2].value").Replace(functions_extension_version);

            foreach (var query in Request.Query.Where(k => k.Key.StartsWith("appsetting_")))
            {
                appsettings.Add(new JObject(
                    new JProperty("name", query.Key.Substring("appsetting_".Length)),
                    new JProperty("value", query.Value.FirstOrDefault())
                    ));
            }

            return Ok(template);
        }


        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/ACI/commands/certificate")]
        public async Task<IActionResult> ACICommand([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName, string secretName)
        {

            var template = await LoadTemplateAsync(endpoints.Value, "ACI.ContainerInstanceCommand.json");

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

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/retrieveCertificate")]
        public async Task<IActionResult> retrieveCertificate([FromServices] IOptions<EndpointOptions> endpoints)
        {

            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.retrieveCertificate.json");
            return Ok(template);

        }
        private static ConcurrentDictionary<string, DateTimeOffset> _delays = new ConcurrentDictionary<string, DateTimeOffset>();

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/ManagedIdentity/roleAssignments")]
        public async Task<IActionResult> DoRoleAssignment([FromServices] IOptions<EndpointOptions> endpoints, string id, string provider, string resourceName, string sourceResource)
        {

            if (!string.IsNullOrEmpty(id))
            {
                var delayUntil = _delays.GetOrAdd(id, DateTimeOffset.UtcNow.AddSeconds(30));

                await Task.Delay(delayUntil.Subtract(DateTimeOffset.UtcNow));
            }

            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.roleAssignments.json");

            if (!string.IsNullOrEmpty(provider))
            {
                template.SelectToken("$.resources[0].type").Replace($"{provider}/providers/roleAssignments");
                template.SelectToken("$.resources[0].name").Replace($"[concat('{resourceName}/Microsoft.Authorization/',guid(resourceGroup().id, '{resourceName}'))]");

                if (!string.IsNullOrEmpty(sourceResource))
                {
                    template.SelectToken("$.resources[0].properties.principalId").Replace($"[reference(concat(resourceId('{provider}','{sourceResource}'),'/providers/Microsoft.ManagedIdentity/Identities/default'),'2018-11-30').principalId]");
                }
            }


            return Ok(template);

        }





        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/retrieveAndParseCertificate")]
        public async Task<IActionResult> retrieveAndParseCertificate([FromServices] IOptions<EndpointOptions> endpoints, string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var delayUntil = _delays.GetOrAdd(id, DateTimeOffset.UtcNow.AddSeconds(60));

                await Task.Delay(delayUntil.Subtract(DateTimeOffset.UtcNow));
            }

            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.retrieveAndParseCertificate.json");
            return Ok(template);

        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/UnprotectValues")]
        public async Task<IActionResult> UnprotectValues([FromServices] IOptions<EndpointOptions> endpoints, [FromServices] IDataProtector dataProtector)
        {
            var values = Request.Query.ToDictionary(k => k.Key, v => new { type = "securestring", value = dataProtector.Unprotect(v.Value.First()) });

            return Content(JObject.FromObject(new Dictionary<string, object>
                {
                    {"$schema" ,"https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#"},
                    {"contentVersion","1.0.0.0" },
                      { "resources",new object[0]},
                    {
                        "outputs", values
                    }
                    }).ToString(Newtonsoft.Json.Formatting.Indented), "application/json");
        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/parseCertificate")]
        public async Task<IActionResult> parseCertificate([FromServices] IOptions<EndpointOptions> endpoints, [FromServices] IDataProtector dataProtector, string certificate, string secrets, bool encrypted = false)
        {

            //            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.parseCertificate.json");

            var cert = new X509Certificate2(Convert.FromBase64String(certificate));
            var secretsObj = null as JObject;
            try
            {
                secrets = secrets.Replace("\\\\\\\\\\\\\\\"", "\"");
                secretsObj = JToken.Parse(secrets) as JObject;

            }
            catch (Exception ex)
            {
                throw new Exception(secrets);
            }

            string encrypt(string value)
            {
                try
                {
                    return string.IsNullOrEmpty(value) ? "" : Encrypt(cert, encrypted ? Encoding.Unicode.GetString(dataProtector.Unprotect(Base64.DecodeToByteArray(value))) : value);
                }
                catch (Exception ex)
                {
                    throw new Exception(value);
                }
            }

            var encryptedSecrets = secretsObj.Properties().ToDictionary(k => k.Name, value => encrypt(value.Value.ToString()));


            return Content(JObject.FromObject(new Dictionary<string, object>
                {
                    {"$schema" ,"https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#"},
                    {"contentVersion","1.0.0.0" },
                      { "resources",new object[0]},
                    {
                        "outputs", new {
                            thumbprint=new {
                                type="string",
                                value=cert.Thumbprint
                            },
                            secrets = new {
                                type="object",
                                value= encryptedSecrets// 
                            }
                        }
                    }
                    }).ToString(Newtonsoft.Json.Formatting.Indented), "application/json");


            //  return Ok(template);

        }

        private string Encrypt(X509Certificate2 cert, string value)
        {

            var encoded = Encoding.Unicode.GetBytes(value);
            var content = new ContentInfo(encoded);
            var env = new EnvelopedCms(content);
            env.Encrypt(new CmsRecipient(cert));
            return Convert.ToBase64String(env.Encode());
        }

        private X509Certificate2 buildSelfSignedServerCertificate(string CertificateName, string password, string dns)
        {

            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            if (!string.IsNullOrEmpty(dns))
            {
                sanBuilder.AddDnsName(dns);
            }
            // 
            //  sanBuilder.AddDnsName(Environment.MachineName);

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={CertificateName}");

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048 * 2, new CspParameters(24, "Microsoft Enhanced RSA and AES Cryptographic Provider", Guid.NewGuid().ToString())))
            {
                rsa.PersistKeyInCsp = false;

                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));


                request.CertificateExtensions.Add(
                   new X509EnhancedKeyUsageExtension(
                       new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));

                request.CertificateExtensions.Add(sanBuilder.Build());


                using (X509Certificate2 cert = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650))))
                {

                    bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                                  .IsOSPlatform(OSPlatform.Windows);
                    if (isWindows)
                        cert.FriendlyName = CertificateName;

                    // Export the PFX using the current key.  Re-import it with no flags to
                    // make it a normal "perphemeral" key behavior.
                    return new X509Certificate2(cert.Export(X509ContentType.Pkcs12), "", X509KeyStorageFlags.Exportable);
                }

                // return new X509Certificate2(certificate.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.MachineKeySet);
            }
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/WebApps/ListHostKeys")]
        public async Task<IActionResult> GetHostKeys()
        {
            //     "template": {
            //  "$schema": "http://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#",
            //  "contentVersion": "1.0.0.0",
            //  "resources": [

            //  ],
            //  "outputs": {
            //    "keys": {
            //      "type": "string",
            //      "value": "[listkeys(concat(, '/host/default/'),'2018-11-01').systemKeys.durabletask_extension]"
            //    }
            //  }
            //}

            return Content(JObject.FromObject(new Dictionary<string, object>
            {
                ["$schema"] = "https://schema.management.azure.com/schemas/2015-01-01/deploymentParameters.json#",
                ["contentVersion"] = "1.0.0.0",
                ["parameters"] = new
                {
                    resourceId = new
                    {
                        type = "string"
                    }
                },
                ["resources"] = new object[0],
                ["outputs"] = new
                {
                    keys=new
                    {
                        tpye="secureobject",
                        value= "[listkeys(concat(parameters('resourceId'), '/host/default/'),'2018-11-01')]"
                    }
                }

            }).ToString(Newtonsoft.Json.Formatting.Indented), "application/json");
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/certificates/demo/parameters")]
        public async Task<IActionResult> GetCertificateParameters(
            [FromServices] IOptions<EndpointOptions> endpoints,
            [FromServices] CloudStorageAccount cloudStorageAccount,
            string keyVaultName, string secretName, string dns)
        {
            var password = "";
            X509Certificate2 x509Certificate = null;



            bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(OSPlatform.Windows);
            //if (isWindows)
            //{
            //    var cert = Certificate.CreateSelfSignCertificatePfx($"CN={keyVaultName}", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), password);
            //    x509Certificate = new X509Certificate2(cert, password, X509KeyStorageFlags.Exportable);
            //    Console.WriteLine($"Certificate {x509Certificate.Issuer} created with thumbprint {x509Certificate.Thumbprint}");

            //}
            //else
            {
                x509Certificate = buildSelfSignedServerCertificate(keyVaultName, password, dns);
                Console.WriteLine($"UNIX:Certificate {x509Certificate.Issuer} created with thumbprint {x509Certificate.Thumbprint}");

            }




            var certBase64 = Convert.ToBase64String(x509Certificate.Export(X509ContentType.Pkcs12));

            var container = cloudStorageAccount.CreateCloudBlobClient().GetContainerReference("publiccerts");
            await container.CreateIfNotExistsAsync();
            var blob = container.GetBlockBlobReference(x509Certificate.Thumbprint + ".cer");
            var publicKey = x509Certificate.Export(X509ContentType.Cert);
            await blob.UploadFromByteArrayAsync(publicKey, 0, publicKey.Length);

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
                    },
                    publicKey=new
                    {
                        value=publicKey
                    },
                    publicKeyLink=new
                    {
                        value=blob.Uri + blob.GetSharedAccessSignature(new SharedAccessBlobPolicy{ Permissions = SharedAccessBlobPermissions.Read, SharedAccessExpiryTime = DateTimeOffset.MaxValue })
                    }

                } }
            }).ToString(Newtonsoft.Json.Formatting.Indented), "application/json");

        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/IpAddresses")]
        public async Task<IActionResult> GetVaultsTemplate([FromServices] IOptions<EndpointOptions> endpoints, int additionalIpAddresses = 0)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "ServiceFabric.ipaddresses.json");

            if (additionalIpAddresses > 0)
            {
                var resources = template.SelectToken("$.resources") as JArray;


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

                }
            }

            return Ok(template);
        }


        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVault/vaults/{keyVaultName}")]
        public async Task<IActionResult> GetVaultsTemplate([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName)
        {
            var template = await LoadTemplateAsync(endpoints.Value, "KeyVault.vault.json");

            if (!string.IsNullOrEmpty(keyVaultName))
            {
                template.SelectToken("$.parameters.keyVaultName")["defaultValue"] = keyVaultName;
            }

            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/KeyVaults/{keyVaultName}/secrets/{secretName}")]
        public async Task<IActionResult> AddSecretTemplate([FromServices] IOptions<EndpointOptions> endpoints, string keyVaultName, string secretName)
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
            template.SelectToken("$.parameters.certificateThumbprint").Parent.Remove();
            template.SelectToken("$.outputs.certificateThumbprint").Parent.Remove();
            template.SelectToken("$.outputs")["secretUriWithVersion"] = template.SelectToken("$.outputs.certificateUrlValue").DeepClone();
            template.SelectToken("$.outputs.certificateUrlValue").Parent.Remove();
            template.SelectToken("$.resources[0].properties.contentType").Parent.Remove();

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
                if (parts[0] == "topic")
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

                foreach (var correlationMapping in correlationMappings)
                {
                    var parts = correlationMapping.Split(":"); //topic:pipelines-signalr:EarthMLPipelines.Signalr
                    switch (parts.First())
                    {
                        case "topic":

                            var topicSubscription = subscriptionTemplate.DeepClone();
                            topicSubscription.SelectToken("$.name").Replace($"sub2{parts[1]}");
                            topicSubscription.SelectToken("$.dependsOn").Replace(JToken.FromObject(new[] { topicName, parts[1] }));
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
        public async Task<IActionResult> GetClusterTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withDocker, string vmImagePublisher = "MicrosoftWindowsServer", int additionalIpAddresses = 0, string durabilityLevel = "Bronze")
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
                // var lbDepsn = lb.SelectToken("$.dependsOn") as JArray;
                var frontendIPConfigurations = lb.SelectToken("$.properties.frontendIPConfigurations") as JArray;

                for (var i = 1; i <= additionalIpAddresses; i++)
                {


                    //resources.Add(JToken.FromObject(new
                    //{
                    //    apiVersion = "[variables('publicIPApiVersion')]",
                    //    type = "Microsoft.Network/publicIPAddresses",
                    //    name = $"[concat(variables('lbIPName'),'-','{i}')]",
                    //    location = "[variables('computeLocation')]",
                    //    properties = new
                    //    {
                    //        publicIPAllocationMethod = "Dynamic",
                    //    },
                    //    tags = new
                    //    {
                    //        resourceType = "Service Fabric",
                    //        clusterName = "[parameters('clusterName')]"
                    //    }
                    //}));








                    //      lbDepsn.Add($"[concat('Microsoft.Network/publicIPAddresses/',concat(variables('lbIPName'),'-','{i}'))]");


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
            var encryptet = await client.PostAsJsonAsync($"{gatewayUrl}/providers/ServiceFabricGateway.Fabric/security/encryptParameter", new
            {
                value = value
            });
            var json = JToken.Parse(await encryptet.Content.ReadAsStringAsync());
            var encryptedBytes = Convert.FromBase64String(json.SelectToken("$.value").ToString());
            var envelope = new EnvelopedCms();
            envelope.Decode(encryptedBytes);

            var RecipientInfo = envelope.RecipientInfos.Cast<RecipientInfo>().First();
            json["recipient"] = JToken.FromObject(RecipientInfo.RecipientIdentifier.Value);
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

            foreach (var obj in applications)
            {
                template.SelectToken("$.outputs")[$"{obj["applicationName"].ToString().Replace("fabric:/", "")}{obj["applicationTypeName"]}{obj["applicationTypeVersion"]}"] = JToken.FromObject(new { type = "bool", value = true });

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
        public async Task<IActionResult> GetVmssTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withdocker, bool? withExtensions, bool withAutoscale = false, string vmImagePublisher = "MicrosoftWindowsServer")
        {

            JToken template = await LoadTemplateAsync(endpoints.Value, "VMSS.vmss.json");


            if (vmImagePublisher == "Canonical")
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

        [HttpGet("providers/DotNetDevOps.AzureTemplates/deploy/applications/{name}")]
        public async Task<IActionResult> DeployRedirect([FromServices] IOptions<EndpointOptions> endpoints, [FromServices] IDistributedCache cache, string name, string userId, [FromServices] IDataProtector dataProtector)
        {
            var url = $"{endpoints.Value.ResourceApiEndpoint}{Request.Path}{Request.QueryString}".Replace("DotNetDevOps.AzureTemplates/deploy/", "DotNetDevOps.AzureTemplates/templates/");

            var template = await LoadTemplateAsync(endpoints.Value, $"Applications.{name.Replace('-', '.')}.json");


            var parameters = template.SelectToken("$.parameters") as JObject;
            foreach (var prop in parameters.Properties())
            {
                var reference = prop.Value.SelectToken("$.reference");
                if (reference != null)
                {
                    var vault = prop.Value.SelectToken("$.reference.keyVault").ToString();
                    var secret = prop.Value.SelectToken("$.reference.secretName").ToString();


                    var redirectUri = "";
                    var client = new KeyVaultClient(async (string authority, string resource, string scope) =>
                    {

                        var id = Guid.NewGuid().ToString("N");
                        userId = userId ?? reference.SelectToken("$.login_hint")?.ToString();
                        var ctx = new AuthenticationContext(authority, new AdalDistributedTokenCache(cache, userId));
                        AuthenticationResult result = null;
                        try
                        {
                            result = await ctx.AcquireTokenSilentAsync(resource, "348f8c76-1a9d-4fcb-bb7b-26f785495226");
                        }
                        catch (AdalException adalException)
                        {
                            if (adalException.ErrorCode == AdalError.FailedToAcquireTokenSilently
                             || adalException.ErrorCode == AdalError.InteractionRequired)
                            {

                                //AuthenticationContext authContext = new AuthenticationContext(authority);
                                //ClientCredential credential = new ClientCredential(clientId, secret);
                                //string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                                //AuthenticationResult result = await authContext.AcquireTokenSilentAsync(resource, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));
                                //

                                var state = Base64.Encode(JsonConvert.SerializeObject(new { authority, resource, scope, redirectUri = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}{HttpContext.Request.Path}{HttpContext.Request.QueryString}" }));
                                var uri = await ctx.GetAuthorizationRequestUrlAsync(resource, "348f8c76-1a9d-4fcb-bb7b-26f785495226", new Uri(
                                    endpoints.Value.ResourceApiEndpoint + "/oidc-signin"), UserIdentifier.AnyUser, $"state={state}&nonce={Guid.NewGuid()}&response_mode=form_post&login_hint={reference.SelectToken("$.login_hint")}");

                                redirectUri = uri.ToString().Replace("response_type=code", "response_type=code id_token");

                                //  await cache.SetAsync("kv_item_" + id, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { authority,resource,scope,redirectUri = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}{HttpContext.Request.Path}{HttpContext.Request.QueryString}" })));

                                throw new Exception("AuthenticationNeeded");




                            }
                        }

                        return result?.AccessToken;
                    });

                    var param = prop.Value;
                    try
                    {
                        var value = await client.GetSecretAsync($"https://{vault.Split('/').Last()}.vault.azure.net", secret);

                        param["defaultValue"] = value.Value;
                        url = url + $"{(url.Contains('?') ? '&' : '?')}{prop.Name}={Base64.Encode(dataProtector.Protect(Encoding.Unicode.GetBytes(value.Value)))}";
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "AuthenticationNeeded")
                        {
                            return Redirect(redirectUri);
                        }
                    }

                    reference.Parent.Remove();

                }
            }



            return Redirect(
                $"https://portal.azure.com/#create/Microsoft.Template/uri/{WebUtility.UrlEncode(url)}");

        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/deploy/{*path}")]
        public async Task<IActionResult> DeployRedirect([FromServices] IOptions<EndpointOptions> endpoints)
        {
            var url = $"{endpoints.Value.ResourceApiEndpoint}{Request.Path}{Request.QueryString}".Replace("DotNetDevOps.AzureTemplates/deploy/", "DotNetDevOps.AzureTemplates/templates/");

            return Redirect(
            $"https://portal.azure.com/#create/Microsoft.Template/uri/{WebUtility.UrlEncode(url)}");

        }
        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/applications/{name}")]
        public async Task<IActionResult> GetApplicationTemplate([FromServices] IOptions<EndpointOptions> endpoints, [FromServices] IDataProtector dataProtector, string name)
        {
            var template = await LoadTemplateAsync(endpoints.Value, $"Applications.{name.Replace('-', '.')}.json", Request.Query);





            return Ok(template);
        }

        [HttpGet("providers/DotNetDevOps.AzureTemplates/templates/demo")]
        public async Task<IActionResult> GetDemoTemplate([FromServices] IOptions<EndpointOptions> endpoints, bool? withApp, bool? withDocker, string vmImagePublisher = "MicrosoftWindowsServer", string gatewayVersionPrefix = "ci")
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
                    template.SelectToken("$.parameters.gatewayVersion.defaultValue").Replace(folders.FirstOrDefault(k => k.StartsWith(gatewayVersionPrefix)));
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
    public static class Base64
    {
        static readonly char[] padding = { '=' };
        public static string Encode(string str)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(str))
            .TrimEnd(padding).Replace('+', '-').Replace('/', '_');
        }
        public static string Encode(byte[] str)
        {
            return System.Convert.ToBase64String(str)
            .TrimEnd(padding).Replace('+', '-').Replace('/', '_');
        }
        public static string Decode(string str)
        {
            string incoming = str
    .Replace('_', '/').Replace('-', '+');
            switch (str.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(incoming);
            return Encoding.UTF8.GetString(bytes);
        }
        public static byte[] DecodeToByteArray(string str)
        {
            string incoming = str
    .Replace('_', '/').Replace('-', '+');
            switch (str.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(incoming);
            return bytes;
        }
    }
}
