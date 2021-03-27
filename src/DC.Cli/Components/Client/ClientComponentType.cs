using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Client
{
    public class ClientComponentType : IComponentType<ClientComponent, ClientComponentType.ComponentData>
    {
        private static readonly IImmutableDictionary<ClientType, Func<DirectoryInfo, ClientComponent.ClientConfiguration, ProjectSettings, Task>> TypeHandlers =
            new Dictionary<ClientType, Func<DirectoryInfo, ClientComponent.ClientConfiguration, ProjectSettings, Task>>
            {
                [ClientType.VueNuxt] = CreateVueNuxt
            }.ToImmutableDictionary();

        
        public async Task<ClientComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var configFile = Path.Combine(tree.Path.FullName, ClientComponent.ConfigFileName);

            if (File.Exists(configFile))
                throw new InvalidOperationException($"You can't create a client at \"{tree.Path.FullName}\". It already exists.");
            
            var clientPort = data.Port ?? ProjectSettings.GetRandomUnusedPort();
            
            var configuration = new ClientComponent.ClientConfiguration
            {
                Name = data.Name,
                Settings = new ClientComponent.ClientConfiguration.ClientSettings
                {
                    Port = clientPort,
                    Type = data.ClientType.ToString(),
                    BaseUrl = data.BaseUrl
                }
            };
            
            await TypeHandlers[data.ClientType](tree.Path, configuration, settings);
            
            var serializer = new Serializer();
            await File.WriteAllTextAsync(Path.Combine(tree.Path.FullName, ClientComponent.ConfigFileName),
                serializer.Serialize(configuration));

            return await ClientComponent.Init(tree.Path, x => CreateBaseContainer(tree.Path, x, settings));
        }

        public async Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var component = await ClientComponent.Init(components.Path, x => CreateBaseContainer(components.Path, x, settings));

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        private static Task CreateVueNuxt(
            DirectoryInfo dir,
            ClientComponent.ClientConfiguration configuration,
            ProjectSettings settings)
        {
            return CreateBaseContainer(dir.Parent, configuration, settings)
                .Temporary()
                .Interactive()
                .Run($"create nuxt-app {dir.Name}");
        }

        private static Docker.Container CreateBaseContainer(
            FileSystemInfo path,
            ClientComponent.ClientConfiguration configuration,
            ProjectSettings settings)
        {
            return Docker
                .ContainerFromImage("node", configuration.GetContainerName(settings))
                .EntryPoint("yarn")
                .WithVolume(path.FullName, "/usr/local/src", true);
        }

        public class ComponentData
        {
            public ComponentData(string name, int? port, ClientType clientType, string baseUrl)
            {
                Name = name;
                Port = port;
                ClientType = clientType;
                BaseUrl = baseUrl;
            }

            public string Name { get; }
            public int? Port { get; }
            public ClientType ClientType { get; }
            public string BaseUrl { get; }
        }
    }
}