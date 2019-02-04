# DotNetDevOps
.Net Devops Website

[![Build status](https://dev.azure.com/dotnet-devops/dotnetdevops/_apis/build/status/dotnetdevops%20CI%20PR)](https://dev.azure.com/dotnet-devops/dotnetdevops/_build/latest?definitionId=1)


## Deployment

The website is deployed on ServiceFabric using a gateway project that used NGINX to route requests to the providers (ie. 'DotNetDevOps.AzureTemplates' ) and setting up a mapping of domain www.dotnetdevops.org with 301 redirect.

The configuration looks like this:
```cs
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
                    SignerEmail = "info@dotnetdevops.org",
                    UseHttp01Challenge = false
                },
                Properties = new Dictionary<string, object> { ["www301"]= true , ["cf-real-ip"]= true ,["CloudFlareZoneId"]="93ff89ba4caa7ea02c70d27ca9fd9e2e" },
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
        });

    await container.Build().RunAsync();
}
```

where the ZoneId is for cloudflare and the api token is retrieved from keyvault. The gateway handle creation of certificates using letsencrypt.

### Deploy Service Fabric Cluster + Gateway
[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://management.dotnetdevops.org/providers/DotNetDevOps.AzureTemplates/deploy/demo?withApp=true)

### Deploy or Update DotNETDevOps 
[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://management.dotnetdevops.org/providers/DotNetDevOps.AzureTemplates/deploy/applications/DotNetDevOps)