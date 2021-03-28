using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using DC.Cli.Components.Terraform;

namespace DC.Cli.Components.Client
{
    public class ClientComponentType : IComponentType<ClientComponent, ClientComponentType.ComponentData>
    {
        private static readonly IImmutableDictionary<ClientType, Func<DirectoryInfo, ProjectSettings, string, Task>> TypeHandlers =
            new Dictionary<ClientType, Func<DirectoryInfo, ProjectSettings, string, Task>>
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

            await TypeHandlers[data.ClientType](tree.Path, settings, data.Name);

            await Templates.Extract(
                ClientComponent.ConfigFileName,
                configFile,
                Templates.TemplateType.Infrastructure,
                ("NAME", TemplateData.SanitizeResourceName(data.Name)),
                ("PORT", clientPort.ToString()),
                ("TYPE", data.ClientType.ToString()),
                ("PATH", settings.GetRelativePath(
                    tree.Path.FullName, 
                    tree.FindFirst<TerraformRootComponent>(Components.Direction.Out)?.FoundAt.Path.FullName)));
            
            return await ClientComponent.Init(tree.Path, x => CreateBaseContainer(tree.Path, settings, x.Name), settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var component = await ClientComponent.Init(components.Path, x => CreateBaseContainer(components.Path, settings, x.Name), settings);

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        private static Task CreateVueNuxt(
            DirectoryInfo dir,
            ProjectSettings settings,
            string name)
        {
            return CreateBaseContainer(dir.Parent, settings, name)
                .Temporary()
                .Interactive()
                .Run($"create nuxt-app {dir.Name}");
        }

        private static Docker.Container CreateBaseContainer(
            FileSystemInfo path,
            ProjectSettings settings,
            string name)
        {
            return Docker
                .ContainerFromImage("node", ClientComponent.ClientConfiguration.GetContainerName(settings, name))
                .EntryPoint("yarn")
                .WithVolume(path.FullName, "/usr/local/src", true);
        }

        public class ComponentData
        {
            public ComponentData(string name, int? port, ClientType clientType)
            {
                Name = name;
                Port = port;
                ClientType = clientType;
            }

            public string Name { get; }
            public int? Port { get; }
            public ClientType ClientType { get; }
        }
    }
}