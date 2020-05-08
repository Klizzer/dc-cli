using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components.Terraform
{
    public class TerraformRootComponentType 
        : IComponentType<TerraformRootComponent, TerraformRootComponentType.ComponentData>
    {
        public async Task<TerraformRootComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var filePath = Path.Combine(tree.Path.FullName, $"{data.Name}.main.tf");

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    $"There is already a terraform root module named {data.Name} at {tree.Path.FullName}");
            }

            await File.WriteAllTextAsync(filePath, "");

            return new TerraformRootComponent(data.Name, tree.Path);
        }

        public Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings)
        {
            var result = from file in path.EnumerateFiles()
                where file.Name.EndsWith(".main.tf")
                select new TerraformRootComponent(Path.GetFileNameWithoutExtension(file.Name), file.Directory);
            
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