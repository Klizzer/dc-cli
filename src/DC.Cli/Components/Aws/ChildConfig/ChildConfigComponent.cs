using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.ChildConfig
{
    public class ChildConfigComponent : ICloudformationComponent
    {
        private readonly ChildConfiguration _configuration;
        
        public ChildConfigComponent(ChildConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string Name => _configuration.Name;
        
        public async Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>>
            GetRequiredConfigurations(Components.ComponentTree components)
        {
            var template = await GetCloudformationData(components);

            return await template.GetRequiredConfigurations();
        }

        public async Task<TemplateData> GetCloudformationData(Components.ComponentTree components)
        {
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component?.GetCloudformationData(x.tree))
                    .WhenAll())
                .Merge();

            var currentTemplate = new TemplateData();

            foreach (var configOverride in _configuration.Overrides)
            {
                var matchingResources = template
                    .Resources
                    .Where(x => x.Value.Type == configOverride.Type)
                    .ToImmutableList();

                foreach (var resource in matchingResources)
                {
                    currentTemplate.Resources[resource.Key] = new TemplateData.ResourceData
                    {
                        Type = resource.Value.Type,
                        Properties = configOverride.Properties
                    };
                }
            }

            return currentTemplate;
        }

        public static async Task<ChildConfigComponent> Init(FileInfo file)
        {
            if (!file.Exists) 
                return null;
            
            var deserializer = new Deserializer();
            return new ChildConfigComponent(
                deserializer.Deserialize<ChildConfiguration>(
                    await File.ReadAllTextAsync(file.FullName)));
        }
        
        public class ChildConfiguration
        {
            public string Name { get; set; }
            public IEnumerable<Override> Overrides { get; set; }
            
            public class Override
            {
                public string Type { get; set; }
                public IDictionary<string, object> Properties { get; set; }
            }
        }
    }
}