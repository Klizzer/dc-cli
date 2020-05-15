using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli.Components.Aws.ApiGateway
{
    public class ApiGatewayComponentType : IComponentType<ApiGatewayComponent, ApiGatewayComponentType.ComponentData>
    {
        public async Task<ApiGatewayComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data, 
            ProjectSettings settings)
        {
            var configPath = Path.Combine(tree.Path.FullName, ApiGatewayComponent.ConfigFileName);
            
            if (File.Exists(configPath))
                throw new InvalidOperationException($"You can't add a new api at: \"{tree.Path.FullName}\". It already exists.");
            
            var port = data.Port ?? ProjectSettings.GetRandomUnusedPort();
            
            await Templates.Extract(
                ApiGatewayComponent.ConfigFileName,
                configPath,
                Templates.TemplateType.Infrastructure,
                ("API_NAME", TemplateData.SanitizeResourceName(data.Name)),
                ("PORT", port.ToString()),
                ("DEFAULT_LANGUAGE", data.DefaultLanguage),
                ("BASE_URL", data.BaseUrl));

            return await ApiGatewayComponent.Init(tree.Path, settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var apiGateway = await ApiGatewayComponent.Init(components.Path, settings);

            return apiGateway != null
                ? new List<IComponent>
                {
                    apiGateway
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        public class ComponentData
        {
            public ComponentData(string name, int? port, string defaultLanguage, string baseUrl)
            {
                Name = name;
                Port = port;
                DefaultLanguage = defaultLanguage;
                BaseUrl = baseUrl;
            }

            public string Name { get; }
            public int? Port { get; }
            public string DefaultLanguage { get; }
            public string BaseUrl { get; }
        }
    }
}