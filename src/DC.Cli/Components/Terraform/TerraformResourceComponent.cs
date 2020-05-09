using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli.Components.Terraform
{
    public class TerraformResourceComponent : IHavePackageResources
    {
        private readonly FileInfo _file;
        private readonly ProjectSettings _settings;

        public TerraformResourceComponent(string name, FileInfo file, ProjectSettings settings)
        {
            Name = name;
            _file = file;
            _settings = settings;
        }

        public string Name { get; }
        
        public async Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components,
            string version)
        {
            var resource = new PackageResource(
                _settings.GetRelativePath(_file.FullName, components.Path.FullName),
                await File.ReadAllBytesAsync(_file.FullName));
            
            return new List<PackageResource>
            {
                resource
            }.ToImmutableList();
        }
    }
}