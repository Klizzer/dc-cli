using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DC.Cli.Components.PackageFiles
{
    public class PackageDirectoryComponentType
        : IComponentType<PackageDirectoryComponent, PackageDirectoryComponent.ComponentData>
    {
        public Task<PackageDirectoryComponent> InitializeAt(
            Components.ComponentTree tree, 
            PackageDirectoryComponent.ComponentData data, 
            ProjectSettings settings)
        {
            var directory = new DirectoryInfo(Path.Combine(tree.Path.FullName, $"{data.Name}.include"));

            if (directory.Exists)
            {
                throw new InvalidOperationException(
                    $"There is already a directory named {data.Name} at {tree.Path.FullName}");
            }

            directory.Create();

            return Task.FromResult(new PackageDirectoryComponent(directory, settings));
        }

        public Task<IImmutableList<IComponent>> FindAt(Components.ComponentTree components, ProjectSettings settings)
        {
            var result = from directory in components.Path.EnumerateDirectories()
                where directory.Name.EndsWith(".include")
                select new PackageDirectoryComponent(directory, settings);
            
            return Task.FromResult<IImmutableList<IComponent>>(result.OfType<IComponent>().ToImmutableList());
        }
    }
}