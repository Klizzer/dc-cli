using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components.Terraform
{
    public class TerraformRootComponent : IPackageApplication
    {
        private readonly DirectoryInfo _path;

        public TerraformRootComponent(string name, DirectoryInfo path)
        {
            Name = name;
            _path = path;
        }
        
        public string Name { get; }
        
        public async Task<PackageResult> Package(IImmutableList<PackageResource> resources, string version)
        {
            var packageResources = resources
                .Add(new PackageResource("main.tf",
                    await File.ReadAllBytesAsync(Path.Combine(_path.FullName, $"{Name}.main.tf"))));
            
            return new PackageResult($"{Name}.{version}.zip", packageResources);
        }
    }
}