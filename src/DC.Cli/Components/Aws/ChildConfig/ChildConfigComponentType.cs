using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DC.Cli.Components.Aws.ChildConfig
{
    public class ChildConfigComponentType : IComponentType<ChildConfigComponent, ChildConfigComponentType.ComponentData>
    {
        public async Task<ChildConfigComponent> InitializeAt(Components.ComponentTree tree, ComponentData data, ProjectSettings settings)
        {
            var configPath = new FileInfo(Path.Combine(tree.Path.FullName, $"{data.Name}.childconfig.yml"));
            
            if (configPath.Exists)
                throw new InvalidOperationException($"You can't add child config {data.Name} at \"{tree.Path.FullName}\". It already exists.");

            await Templates.Extract(
                "childconfig.yml",
                configPath.FullName,
                Templates.TemplateType.Infrastructure,
                ("NAME", TemplateData.SanitizeResourceName(data.Name)));

            return await ChildConfigComponent.Init(configPath);
        }

        public async Task<IImmutableList<IComponent>> FindAt(Components.ComponentTree components, ProjectSettings settings)
        {
            var foundComponents = new List<IComponent>();

            foreach (var file in components.Path.EnumerateFiles().Where(x => x.Name.EndsWith(".childconfig.yml")))
            {
                var component = await ChildConfigComponent.Init(file);
                
                if (component != null)
                    foundComponents.Add(component);
            }

            return foundComponents.ToImmutableList();
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