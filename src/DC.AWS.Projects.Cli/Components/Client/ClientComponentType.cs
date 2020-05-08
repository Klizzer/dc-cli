using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components.Client
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
            var dir = new DirectoryInfo(Path.Combine(tree.Path.FullName, data.Name));
            
            if (dir.Exists)
                throw new InvalidOperationException($"You can't create a client at \"{dir.FullName}\". It already exists.");
            
            dir.Create();

            var clientPort = data.Port ?? ProjectSettings.GetRandomUnusedPort();

            await Templates.Extract(
                ClientComponent.ConfigFileName,
                Path.Combine(dir.FullName, ClientComponent.ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("CLIENT_NAME", dir.Name),
                ("PORT", clientPort.ToString()),
                ("CLIENT_TYPE", data.ClientType.ToString()),
                ("BASE_URL", data.BaseUrl));
            
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
            
            await TypeHandlers[data.ClientType](dir, configuration, settings);
            
            var serializer = new Serializer();
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, ClientComponent.ConfigFileName),
                serializer.Serialize(configuration));

            return await ClientComponent.Init(dir, x => CreateBaseContainer(dir, x, settings));
        }

        public async Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings)
        {
            var component = await ClientComponent.Init(path, x => CreateBaseContainer(path, x, settings));

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
                .Port(configuration.Settings.Port, 3000)
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