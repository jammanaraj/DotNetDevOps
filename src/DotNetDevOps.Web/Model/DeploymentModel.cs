using System;
using System.Collections.Generic;

namespace DotNetDevOps.Web
{
    public class DeploymentModel
    {
        public bool DeleteIfExists { get; set; }
        public string RemoteUrl { get; set; }
        public string ApplicationTypeName { get; set; }
        public string ApplicationTypeVersion { get; set; }
        public string ApplicationName { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        public ServiceDeploymentModel[] ServiceDeployments { get; set; } = Array.Empty<ServiceDeploymentModel>();
    }
}
