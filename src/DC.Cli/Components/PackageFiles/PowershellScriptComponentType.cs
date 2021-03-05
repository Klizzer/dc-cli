using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DC.Cli.Components.PackageFiles
{
    public class PackageFileComponentType 
        : IComponentType<PackageFileScriptComponent, PackageFileComponentType.ComponentData>
    {
        public async Task<PackageFileScriptComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var filePath = Path.Combine(tree.Path.FullName, data.Name);

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    $"There is already a file named {data.Name} at {tree.Path.FullName}");
            }

            await File.WriteAllTextAsync(filePath, "");

            return new PackageFileScriptComponent(data.Name, new FileInfo(filePath), settings);
        }

        public Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var result = from file in components.Path.EnumerateFiles()
                where file.Name.Contains(".include.")
                select new PackageFileScriptComponent(Path.GetFileNameWithoutExtension(file.Name), file, settings);
            
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