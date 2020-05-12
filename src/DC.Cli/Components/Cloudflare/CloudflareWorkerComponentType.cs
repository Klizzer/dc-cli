using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Cloudflare
{
    public class CloudflareWorkerComponentType 
        : IComponentType<CloudflareWorkerComponent, CloudflareWorkerComponentType.ComponentData>
    {
        public async Task<CloudflareWorkerComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var configFilePath = Path.Combine(tree.Path.FullName, CloudflareWorkerComponent.ConfigFileName);
            
            if (File.Exists(configFilePath))
                throw new InvalidOperationException($"There is already a cloudflare worker at {tree.Path.FullName}");
            
            var configuration = new CloudflareWorkerComponent.CloudflareWorkerConfiguration
            {
                Name = data.Name,
                Settings = new CloudflareWorkerComponent.CloudflareWorkerConfiguration.CloudflareWorkerSettings
                {
                    Port = data.Port ?? ProjectSettings.GetRandomUnusedPort(),
                    DestinationPort = data.DestinationPort
                }
            };
            
            var serializer = new Serializer();

            await File.WriteAllTextAsync(configFilePath, serializer.Serialize(configuration));
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            await Directories.Copy(
                Path.Combine(executingAssembly.GetPath(), "Source/CloudflareWorker"), 
                tree.Path.FullName,
                ("WORKER_NAME", data.Name));

            return await CloudflareWorkerComponent.Init(tree.Path, settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings)
        {
            var component = await CloudflareWorkerComponent.Init(path, settings);

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        public class ComponentData
        {
            public ComponentData(string name, int? port, int destinationPort)
            {
                Name = name;
                Port = port;
                DestinationPort = destinationPort;
            }

            public string Name { get; }
            public int? Port { get; }
            public int DestinationPort { get; }
        }
    }
}