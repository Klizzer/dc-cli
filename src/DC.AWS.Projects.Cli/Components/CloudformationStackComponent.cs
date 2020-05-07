using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class CloudformationStackComponent : IComponent
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        
        private static readonly IImmutableDictionary<string, Func<string, TemplateData.ResourceData, CloudformationStackConfiguration, Task>> TypeHandlers =
            new Dictionary<string, Func<string, TemplateData.ResourceData, CloudformationStackConfiguration, Task>>
            {
                ["AWS::DynamoDB::Table"] = HandleTable
            }.ToImmutableDictionary();

        private static readonly IImmutableDictionary<string, int> InternalPorts = new Dictionary<string, int>().ToImmutableDictionary();
        
        private const string ConfigFileName = "cloudformation-stack.config.yml";

        private readonly CloudformationStackConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;

        private CloudformationStackComponent(CloudformationStackConfiguration configuration, ProjectSettings settings)
        {
            _configuration = configuration;
            
            var dataDir = new DirectoryInfo(configuration.GetDataDir(settings));
            
            if (!dataDir.Exists)
                dataDir.Create();
            
            //TODO: Set api key
            _dockerContainer = Docker
                .ContainerFromImage(
                    $"localstack/localstack:{configuration.Settings.LocalstackVersion}",
                    configuration.GetContainerName())
                .Detached()
                .WithVolume(dataDir.FullName, "/tmp/localstack")
                .WithDockerSocket()
                .Port(configuration.Settings.MainPort, 8080)
                .Port(configuration.Settings.ServicesPort, 4566)
                .EnvironmentVariable("SERVICES", string.Join(",", configuration.Settings.Services))
                .EnvironmentVariable("DATA_DIR", "/tmp/localstack/data")
                .EnvironmentVariable("LAMBDA_REMOTE_DOCKER", "0")
                .EnvironmentVariable("DEBUG", "1");
        }

        public string Name => _configuration.Name;
        public int MainPort => _configuration.Settings.MainPort;

        public static async Task InitAt(
            ProjectSettings settings,
            string path,
            string name,
            int mainPort,
            int servicesPort,
            IImmutableList<string> services)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (!dir.Exists)
                dir.Create();
            
            if (File.Exists(Path.Combine(dir.FullName, ConfigFileName)))
                throw new InvalidCastException($"There is already a localstack configured at: \"{dir.FullName}\"");
            
            var serializer = new Serializer();
            
            var configuration = new CloudformationStackConfiguration
            {
                Name = name,
                Settings = new CloudformationStackConfiguration.LocalstackSettings
                {
                    Services = services,
                    MainPort = mainPort,
                    ServicesPort = servicesPort
                }
            };

            await File.WriteAllTextAsync(
                Path.Combine(dir.FullName, ConfigFileName),
                serializer.Serialize(configuration));
        }

        public Task<ComponentActionResult> Restore()
        {
            return Task.FromResult(new ComponentActionResult(true, ""));
        }

        public Task<ComponentActionResult> Build()
        {
            return Task.FromResult(new ComponentActionResult(true, ""));
        }

        public Task<ComponentActionResult> Test()
        {
            return Task.FromResult(new ComponentActionResult(true, ""));
        }

        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            var startResult = await _dockerContainer.Run("");
            
            if (startResult.exitCode != 0)
                return new ComponentActionResult(false, startResult.output);

            var started = await WaitForStart(TimeSpan.FromSeconds(30));
            
            if (!started)
                return new ComponentActionResult(false, "Can't start localstack within 30 seconds.");
            
            var cloudformationComponents =
                components.FindAll<ICloudformationComponent>(Components.Direction.In);

            foreach (var component in cloudformationComponents)
            {
                var template = await component.component.GetCloudformationData();

                foreach (var resource in template.Resources)
                {
                    if (TypeHandlers.ContainsKey(resource.Value.Type))
                        await TypeHandlers[resource.Value.Type](resource.Key, resource.Value, _configuration);
                }
            }
            
            return new ComponentActionResult(true, startResult.output);
        }

        public Task<ComponentActionResult> Stop()
        {
            Docker.Stop(_dockerContainer.Name);
            Docker.Remove(_dockerContainer.Name);

            return Task.FromResult(new ComponentActionResult(true, ""));
        }

        public async Task<ComponentActionResult> Logs()
        {
            var result = await Docker.Logs(_dockerContainer.Name);

            return new ComponentActionResult(true, result);
        }

        private async Task<bool> WaitForStart(TimeSpan timeout)
        {
            var requiredServices = _configuration.GetConfiguredServices();
            
            var timer = Stopwatch.StartNew();

            while (timer.Elapsed < timeout)
            {
                try
                {
                    var response = await HttpClient.GetAsync($"http://localhost:{_configuration.Settings.MainPort}/health");

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = Json.DeSerialize<HealthResponse>(await response.Content.ReadAsStringAsync());

                        if (responseData.Services.Any() 
                            && requiredServices.All(x => responseData.Services.ContainsKey(x) && responseData.Services[x] == "running"))
                        {
                            Console.WriteLine($"Running services: {string.Join(", ", responseData.Services.Keys)}");
                        
                            return true;
                        }

                        Console.WriteLine("Localstack still not running...");
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Localstack still not running...");
                }
                
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            return false;
        }
        
        private static async Task HandleTable(
            string name,
            TemplateData.ResourceData tableNode,
            CloudformationStackConfiguration configuration)
        {
            if (!configuration.Settings.Services.Contains("dynamodb"))
                return;

            var client = new AmazonDynamoDBClient(new BasicAWSCredentials("key", "secret-key"), new AmazonDynamoDBConfig
            {
                ServiceURL = $"http://localhost:{configuration.Settings.ServicesPort}"
            });

            var billingMode = BillingMode.FindValue(tableNode.Properties["BillingMode"].ToString());

            var attributeDefinitions = ((IEnumerable<IDictionary<string, string>>)tableNode.Properties["AttributeDefinitions"])
                .Select(x => new AttributeDefinition(
                    x["AttributeName"],
                    ScalarAttributeType.FindValue(x["AttributeType"])))
                .ToList();

            if (await client.TableExists(name))
            {
                await client.UpdateTableAsync(new UpdateTableRequest
                {
                    TableName = name,
                    AttributeDefinitions = attributeDefinitions,
                    BillingMode = billingMode
                });
            }
            else
            {
                await client.CreateTableAsync(new CreateTableRequest
                {
                    TableName = name,
                    KeySchema = ((IEnumerable<IDictionary<string, string>>)tableNode.Properties["KeySchema"])
                        .Select(x => new KeySchemaElement(
                            x["AttributeName"],
                            KeyType.FindValue(x["KeyType"])))
                        .ToList(),
                    AttributeDefinitions = attributeDefinitions,
                    BillingMode = billingMode
                });
            }
        }

        public static IEnumerable<CloudformationStackComponent> FindAtPath(DirectoryInfo path, ProjectSettings settings)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new CloudformationStackComponent(
                deserializer.Deserialize<CloudformationStackConfiguration>(
                    File.ReadAllText(Path.Combine(path.FullName, ConfigFileName))),
                settings);
        }
        
        private class CloudformationStackConfiguration
        {
            private static readonly IImmutableDictionary<string, IImmutableList<string>> ServiceAliases = 
                new Dictionary<string, IImmutableList<string>>
                {
                    ["serverless"] = new List<string>
                    {
                        "iam",
                        "lambda",
                        "dynamodb",
                        "apigateway",
                        "s3",
                        "sns"
                    }.ToImmutableList()
                }.ToImmutableDictionary();
            
            public string Name { get; set; }
            public LocalstackSettings Settings { get; set; }

            public string GetContainerName()
            {
                //TODO: Use better name
                return Name;
            }

            public string GetDataDir(ProjectSettings settings)
            {
                return settings.GetRootedPath($"./localstack/{Name}");
            }

            public IImmutableList<string> GetConfiguredServices()
            {
                var services = new List<string>();

                foreach (var service in Settings.Services)
                {
                    if (ServiceAliases.ContainsKey(service))
                        services.AddRange(ServiceAliases[service]);
                    else
                        services.Add(service);
                }

                return services.ToImmutableList();
            }
            
            public class LocalstackSettings
            {
                public int MainPort { get; set; }
                public int ServicesPort { get; set; }
                public string LocalstackVersion { get; set; } = "latest";
                public IImmutableList<string> Services { get; set; }
            }
        }
        
        private class HealthResponse
        {
            public IImmutableDictionary<string, string> Services { get; set; }
        }
    }
}