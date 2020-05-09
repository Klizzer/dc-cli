using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DC.Cli.Components.Terraform
{
    public class TerraformResourceComponentType 
        : IComponentType<TerraformResourceComponent, TerraformResourceComponentType.ComponentData>
    {
        public async Task<TerraformResourceComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var filePath = Path.Combine(tree.Path.FullName, $"{data.Name}.tf");

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    $"There is already a terraform module named {data.Name} at {tree.Path.FullName}");
            }

            await File.WriteAllTextAsync(filePath, "");

            return new TerraformResourceComponent(data.Name, new FileInfo(filePath), settings);
        }

        public Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings)
        {
            var result = from file in path.EnumerateFiles()
                where file.Name.EndsWith(".tf") && !file.Name.EndsWith(".main.tf")
                select new TerraformResourceComponent(Path.GetFileNameWithoutExtension(file.Name), file, settings);
            
            return Task.FromResult<IImmutableList<IComponent>>(result.OfType<IComponent>().ToImmutableList());
        }
        
        public class ComponentData
        {
            public ComponentData(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
}