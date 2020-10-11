using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DC.Cli.Components.Aws.LambdaLayer
{
    public class LambdaLayerComponentType
        : IComponentType<LambdaLayerComponent, LambdaLayerComponentType.ComponentData>
    {
        public async Task<LambdaLayerComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var configFilePath = Path.Combine(tree.Path.FullName, LambdaLayerComponent.ConfigFileName);
            
            if (File.Exists(configFilePath))
                throw new InvalidOperationException($"You can't add a new lambda layer at: \"{tree.Path.FullName}\". It already exists.");
            
            var languageVersion = FunctionLanguage.Parse(data.Language);

            var executingAssembly = Assembly.GetExecutingAssembly();

            await Directories.Copy(
                Path.Combine(executingAssembly.GetPath(), $"Templates/LambdaLayers/{languageVersion.Language}"), 
                tree.Path.FullName);

            return await LambdaLayerComponent.Init(tree.Path, settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var component = await LambdaLayerComponent.Init(components.Path, settings);

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        public class ComponentData
        {
            public ComponentData(string name, string language)
            {
                Name = name;
                Language = language;
            }

            public string Name { get; }
            public string Language { get; }
        }
    }
}