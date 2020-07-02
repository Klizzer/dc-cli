using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DC.Cli.Components.Terraform;

namespace DC.Cli.Components.Powershell
{
    public class PowershellScriptComponentType 
        : IComponentType<PowershellScriptComponent, PowershellScriptComponentType.ComponentData>
    {
        public async Task<PowershellScriptComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var filePath = Path.Combine(tree.Path.FullName, $"{data.Name}.ps1");

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    $"There is already a powershell script named {data.Name} at {tree.Path.FullName}");
            }

            await File.WriteAllTextAsync(filePath, "");

            return new PowershellScriptComponent(data.Name, new FileInfo(filePath), settings);
        }

        public Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var result = from file in components.Path.EnumerateFiles()
                where file.Name.EndsWith(".ps1")
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