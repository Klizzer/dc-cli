namespace DC.Cli.Components
{
    public class PackageResource
    {
        public PackageResource(string resourceName, byte[] resourceContent)
        {
            ResourceName = resourceName;
            ResourceContent = resourceContent;
        }

        public string ResourceName { get; }
        public byte[] ResourceContent { get; }
    }
}