using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli.Components.PackageFiles
{
    public class PackageDirectoryComponent : IHavePackageResources
    {
        private readonly DirectoryInfo _directory;
        private readonly ProjectSettings _settings;

        public PackageDirectoryComponent(DirectoryInfo directory, ProjectSettings settings)
        {
            _directory = directory;
            _settings = settings;

            Name = directory.Name;
        }

        public string Name { get; }
        
        public Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components, 
            string version)
        {
            async Task<IImmutableList<PackageResource>> GetFrom(DirectoryInfo directory)
            {
                var result = new List<PackageResource>();

                foreach (var file in directory.GetFiles())
                {
                    result.Add(new PackageResource(
                        _settings.GetRelativePath(file.FullName, components.Path.FullName),
                        await File.ReadAllBytesAsync(file.FullName)));
                }

                foreach (var subDirectory in directory.GetDirectories())
                    result.AddRange(await GetFrom(subDirectory));

                return result.ToImmutableList();
            }

            return GetFrom(_directory);
        }
        
        public class ComponentData
        {
            public string Name { get; set; }
        }
    }
}