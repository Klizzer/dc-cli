using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components.Aws.CloudformationTemplate
{
    public class CloudformationComponentType : IComponentType<CloudformationComponent, CloudformationComponentType.ComponentData>
    {
        public async Task<CloudformationComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var filePath = Path.Combine(tree.Path.FullName, $"{data.Name}.cf.yml");

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    $"There is already a cloudformation template named {data.Name} at {tree.Path.FullName}");
            }
            
            var template = new TemplateData();
            var serializer = new Serializer();

            await File.WriteAllTextAsync(filePath, serializer.Serialize(template));

            return new CloudformationComponent(tree.Path, Path.GetFileName(filePath));
        }

        public Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings)
        {
            var result = (from file in path.EnumerateFiles()
                    where file.Name.EndsWith(".cf.yml")
                    select new CloudformationComponent(path, file.Name))
                .Cast<IComponent>()
                .ToImmutableList();

            return Task.FromResult<IImmutableList<IComponent>>(result);
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