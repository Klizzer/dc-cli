using System.Collections.Immutable;

namespace DC.Cli.Components
{
    public class PackageResult
    {
        public PackageResult(string packageName, IImmutableList<PackageResource> resources)
        {
            PackageName = packageName;
            Resources = resources;
        }

        public string PackageName { get; }
        public IImmutableList<PackageResource> Resources { get; }
    }
}