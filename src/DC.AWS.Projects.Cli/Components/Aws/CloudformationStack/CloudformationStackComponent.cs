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

namespace DC.AWS.Projects.Cli.Components.Aws.CloudformationStack
{
    public class CloudformationStackComponent : 
        IStartableComponent, 
        IComponentWithLogs,
        IHavePackageResources,
        INeedConfiguration
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        
        private static readonly IImmutableDictionary<string, Func<string, TemplateData.ResourceData, CloudformationStackConfiguration, Task>> TypeHandlers =
            new Dictionary<string, Func<string, TemplateData.ResourceData, CloudformationStackConfiguration, Task>>
            {
                ["AWS::DynamoDB::Table"] = HandleTable
            }.ToImmutableDictionary();
        
        public const string ConfigFileName = "cloudformation-stack.config.yml";

        private readonly CloudformationStackConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;
        private readonly DirectoryInfo _path;
        private readonly ProjectSettings _projectSettings;

        private CloudformationStackComponent(
            CloudformationStackConfiguration configuration,
            ProjectSettings settings,
            DirectoryInfo path, 
            ProjectSettings projectSettings)
        {
            _configuration = configuration;
            _path = path;
            _projectSettings = projectSettings;

            var dataDir = new DirectoryInfo(configuration.GetDataDir(settings));
            
            if (!dataDir.Exists)
                dataDir.Create();
            
            _dockerContainer = Docker
                .ContainerFromImage(
                    $"localstack/localstack:{configuration.Settings.LocalstackVersion}",
                    configuration.GetContainerName(projectSettings))
                .Detached()
                .WithVolume(dataDir.FullName, "/tmp/localstack")
                .WithDockerSocket()
                .Port(configuration.Settings.MainPort, 8080)
                .Port(configuration.Settings.ServicesPort, 4566)
                .EnvironmentVariable("SERVICES", string.Join(",", configuration.Settings.Services))
                .EnvironmentVariable("DATA_DIR", "/tmp/localstack/data")
                .EnvironmentVariable("LAMBDA_REMOTE_DOCKER", "0")
                .EnvironmentVariable("DEBUG", "1")
                .EnvironmentVariable("LOCALSTACK_API_KEY", projectSettings.GetConfiguration("localstackApiKey"));
        }

        public string Name => _configuration.Name;
        
        public IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)> GetRequiredConfigurations()
        {
            yield return ("localstackApiKey", "Enter your localstack api key if you have any:",
                INeedConfiguration.ConfigurationType.User);
        }

        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            await Stop();
            
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

            return Task.FromResult(new ComponentActionResult(true, $"Localstack \"{_configuration.Name}\" stopped"));
        }

        public async Task<ComponentActionResult> Logs()
        {
            var result = await Docker.Logs(_dockerContainer.Name);

            return new ComponentActionResult(true, result);
        }
        
        public async Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components,
            string version)
        {
            var tempDir = new DirectoryInfo(Path.Combine(_path.FullName, ".tmp"));
            
            if (tempDir.Exists)
                tempDir.Delete(true);
            
            tempDir.Create();
            
            var outputDir = new DirectoryInfo(Path.Combine(tempDir.FullName, "output"));
            
            outputDir.Create();
            
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component.GetCloudformationData())
                    .WhenAll())
                .Merge();
            
            var serializer = new Serializer();

            await File.WriteAllTextAsync(
                Path.Combine(tempDir.FullName, "template.yml"),
                serializer.Serialize(template));

            var cliDocker = Docker.TemporaryContainerFromImage("amazon/aws-cli")
                .WithVolume("~/.aws", "/root/.aws")
                .WithVolume(
                    _projectSettings.GetRootedPath(""),
                    $"/usr/src/app/${_projectSettings.GetRelativePath(_path.FullName)}")
                .WorkDir("/usr/src/app");

            await cliDocker
                .WithVolume(
                    _projectSettings.GetRootedPath("infrastructure/deployment-bucket.yml"),
                    "/usr/src/app/template.yml")
                .WithVolume(tempDir.FullName, "/usr/src/app/output")
                .Run(@$"cloudformation deploy 
                                        --stack-name {_configuration.Settings.DeploymentStackName} 
                                        --parameter-overrides DeploymentBucketName={_configuration.Settings.DeploymentBucketName} 
                                        --no-fail-on-empty-changeset 
                                        --region {_configuration.Settings.AwsRegion}");

            await cliDocker
                .WithVolume(
                    _projectSettings.GetRootedPath(""),
                    $"/usr/src/app/${_projectSettings.GetRelativePath(_path.FullName)}")
                .WithVolume(outputDir.FullName, "/usr/src/app/output")
                .WithVolume(Path.Combine(tempDir.FullName, "template.yml"), "/usr/src/app/template.yml")
                .Run(@$"cloudformation package 
                                        --output-template-file ./output/template.yml
                                        --s3-bucket {_configuration.Settings.DeploymentBucketName}
                                        --s3-prefix {version}");
            
            var result = new List<PackageResource>();

            foreach (var file in outputDir.EnumerateFiles())
            {
                result.Add(new PackageResource(
                    _projectSettings.GetRelativePath(file.FullName, components.Path.FullName),
                    await File.ReadAllBytesAsync(file.FullName)));
            }
            
            tempDir.Delete();

            return result.ToImmutableList();
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
        
        public static async Task<CloudformationStackComponent> Init(DirectoryInfo path, ProjectSettings settings)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName)))
                return null;
            
            var deserializer = new Deserializer();
            return new CloudformationStackComponent(
                deserializer.Deserialize<CloudformationStackConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName))),
                settings,
                path,
                settings);
        }
        
        public class CloudformationStackConfiguration
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
            public CloudformationStackSettings Settings { get; set; }

            public string GetContainerName(ProjectSettings settings)
            {
                return $"{settings.GetProjectName()}-localstack-{Name}";
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
            
            public class CloudformationStackSettings
            {
                public int MainPort { get; set; }
                public int ServicesPort { get; set; }
                public string AwsRegion { get; set; }
                public string LocalstackVersion { get; set; } = "latest";
                public IImmutableList<string> Services { get; set; }
                public string DeploymentBucketName { get; set; }
                public string DeploymentStackName { get; set; }
            }
        }
        
        private class HealthResponse
        {
            public IImmutableDictionary<string, string> Services { get; set; }
        }
    }
}