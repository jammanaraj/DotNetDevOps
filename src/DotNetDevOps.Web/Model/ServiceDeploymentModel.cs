namespace DotNetDevOps.Web
{
    public class ServiceDeploymentModel
    {
        public string ServiceTypeName { get; set; }
        public string ServiceName { get; set; }
        public byte[] InitializationData { get; set; }
    }
}
