using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.CloudformationStack
{
    public class CloudformationStackComponentType 
        : IComponentType<CloudformationStackComponent, CloudformationStackComponentType.ComponentData>
    {
        public async Task<CloudformationStackComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var configFilePath = Path.Combine(tree.Path.FullName, CloudformationStackComponent.ConfigFileName);
            
            if (File.Exists(configFilePath))
                throw new InvalidCastException($"There is already a cloudformation stack configured at: \"{tree.Path.FullName}\"");
            
            var serializer = new Serializer();
            
            var configuration = new CloudformationStackComponent.CloudformationStackConfiguration
            {
                Name = data.Name,
                Settings = new CloudformationStackComponent.CloudformationStackConfiguration.CloudformationStackSettings
                {
                    Services = data.Services.ToList(),
                    MainPort = data.MainPort,
                    ServicesPort = data.ServicesPort,
                    DeploymentBucketName = $"{data.Name}-deployments",
                    DeploymentStackName = $"{data.Name}-deployments",
                    AwsRegion = data.AwsRegion
                }
            };

            await File.WriteAllTextAsync(
                configFilePath,
                serializer.Serialize(configuration));

            return await CloudformationStackComponent.Init(tree.Path, settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings)
        {
            var component = await CloudformationStackComponent.Init(path, settings);

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        public class ComponentData
        {
            public ComponentData(
                string name, 
                IImmutableList<string> services,
                int mainPort,
                int servicesPort, 
                string awsRegion)
            {
                Name = name;
                Services = services;
                MainPort = mainPort;
                ServicesPort = servicesPort;
                AwsRegion = awsRegion;
            }

            public string Name { get; }
            public IImmutableList<string> Services { get; }
            public int MainPort { get; }
            public int ServicesPort { get; }
            public string AwsRegion { get; }
        }
    }
}