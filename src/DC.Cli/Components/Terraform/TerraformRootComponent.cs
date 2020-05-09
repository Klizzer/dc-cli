using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli.Components.Terraform
{
    public class TerraformRootComponent : IPackageApplication
    {
        private readonly FileInfo _file;

        public TerraformRootComponent(string name, FileInfo file)
        {
            _file = file;
            Name = name;
        }
        
        public string Name { get; }
        
        public async Task<PackageResult> Package(IImmutableList<PackageResource> resources, string version)
        {
            var packageResources = resources
                .Add(new PackageResource("main.tf",
                    await File.ReadAllBytesAsync(_file.FullName)));
            
            return new PackageResult($"{Name}.{version}.zip", packageResources);
        }
    }
}