using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.CloudformationStack
{
    public class CloudformationStackComponent : 
        IStartableComponent, 
        IComponentWithLogs,
        IHavePackageResources,
        INeedConfiguration,
        IParseCloudformationValues
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        
        public const string ConfigFileName = "cloudformation-stack.config.yml";

        private readonly CloudformationStackConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;
        private readonly DirectoryInfo _path;
        private readonly ProjectSettings _projectSettings;
        private readonly DirectoryInfo _tempDir;
        private readonly Components.ComponentTree _components;
        private bool _isStarting;

        private CloudformationStackComponent(
            CloudformationStackConfiguration configuration,
            ProjectSettings settings,
            DirectoryInfo path, 
            ProjectSettings projectSettings, 
            Components.ComponentTree components)
        {
            _configuration = configuration;
            _path = path;
            _projectSettings = projectSettings;
            _components = components;
            _tempDir = new DirectoryInfo(Path.Combine(path.FullName, ".tmp"));

            var dataDir = new DirectoryInfo(configuration.GetDataDir(settings));
            
            if (!dataDir.Exists)
                dataDir.Create();
            
            var container = Docker
                .ContainerFromImage(
                    $"localstack/localstack:{configuration.Settings.LocalstackVersion}",
                    configuration.GetContainerName(projectSettings),
                    false)
                .Detached()
                .WithVolume(dataDir.FullName, "/tmp/localstack")
                .WithDockerSocket()
                .Port(configuration.Settings.MainPort, 8080)
                .Port(configuration.Settings.ServicesPort, 4566)
                .EnvironmentVariable("SERVICES", string.Join(",", (configuration.Settings.Services ?? new List<string>())))
                .EnvironmentVariable("DATA_DIR", "/tmp/localstack/data")
                .EnvironmentVariable("LAMBDA_REMOTE_DOCKER", "0")
                .EnvironmentVariable("DEBUG", "1")
                .EnvironmentVariable("LOCALSTACK_API_KEY", projectSettings.GetConfiguration("localstackApiKey"));

            _dockerContainer = configuration
                .Settings
                .PortMappings
                .Aggregate(container, (current, portMapping) => current.WithArgument($"-p {portMapping}"));
        }

        public string Name => _configuration.Name;
        
        public Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations(Components.ComponentTree components)
        {
            return Task.FromResult<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>>(
                new List<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>
            {
                ("localstackApiKey", "Enter your localstack api key if you have any:",
                    INeedConfiguration.ConfigurationType.User)
            });
        }

        public async Task<bool> Start(Components.ComponentTree components)
        {
            var startedServices = _configuration.GetConfiguredServices();

            if (!startedServices.Any())
                return true;

            _isStarting = true;
            
            var startResult = await _dockerContainer.Run("");

            if (!startResult)
            {
                _isStarting = false;
                return false;
            }

            var started = await WaitForStart(TimeSpan.FromMinutes(1));

            if (!started)
            {
                _isStarting = false;
                
                return false;
            }
            
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component?.GetCloudformationData(x.tree))
                    .WhenAll())
                .Merge();

            await CloudformationResources.EnsureResourcesExist(template, _configuration, _projectSettings);

            _isStarting = false;
            
            return true;
        }

        public Task<bool> Stop()
        {
            Docker.Remove(_dockerContainer.Name);

            return Task.FromResult(true);
        }

        public async Task<bool> Logs()
        {
            var result = await Docker.Logs(_dockerContainer.Name);

            Console.WriteLine(result);
            
            return true;
        }
        
        public async Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components,
            string version)
        {
            if (_tempDir.Exists)
                _tempDir.Delete(true);
            
            _tempDir.Create();
            
            var outputDir = new DirectoryInfo(Path.Combine(_tempDir.FullName, "output"));
            
            outputDir.Create();
            
            var template = (await _components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component?.GetCloudformationData(x.tree))
                    .WhenAll())
                .Merge();
            
            var serializer = new Serializer();

            await File.WriteAllTextAsync(
                Path.Combine(_tempDir.FullName, "template.yml"),
                serializer.Serialize(template));

            var cliDocker = Docker.TemporaryContainerFromImage("amazon/aws-cli", false)
                .WithVolume(Path.Combine(User.GetHome(), ".aws"), "/root/.aws")
                .WithVolume(
                    _projectSettings.GetRootedPath(_path.FullName),
                    $"/usr/src/app/{_projectSettings.GetRelativePath(_path.FullName)}")
                .EnvironmentVariable("AWS_ACCESS_KEY_ID", Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"))
                .EnvironmentVariable("AWS_SECRET_ACCESS_KEY", Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"))
                .WorkDir("/usr/src/app");

            await Templates.Extract(
                "deployment-bucket.yml",
                Path.Combine(_tempDir.FullName, "deployment-bucket.yml"),
                Templates.TemplateType.Infrastructure);
            
            await cliDocker
                .WithVolume(
                    Path.Combine(_tempDir.FullName, "deployment-bucket.yml"),
                    "/usr/src/app/template.yml")
                .Run($"cloudformation deploy --template-file ./template.yml --stack-name {_configuration.Settings.DeploymentStackName} --parameter-overrides DeploymentBucketName={_configuration.Settings.DeploymentBucketName} --no-fail-on-empty-changeset --region {_configuration.Settings.AwsRegion}");

            await cliDocker
                .WithVolume(outputDir.FullName, "/usr/src/app/output")
                .WithVolume(Path.Combine(_tempDir.FullName, "template.yml"), "/usr/src/app/template.yml")
                .Run($"cloudformation package --template-file ./template.yml --output-template-file ./output/template.yml --s3-bucket {_configuration.Settings.DeploymentBucketName} --s3-prefix {version} --region {_configuration.Settings.AwsRegion}");

            var result = new List<PackageResource>();

            foreach (var file in outputDir.EnumerateFiles())
            {
                result.Add(new PackageResource(
                    _projectSettings.GetRelativePath(Path.Combine(_path.FullName, file.Name), components.Path.FullName),
                    await File.ReadAllBytesAsync(file.FullName)));
            }
            
            _tempDir.Delete(true);

            return result.ToImmutableList();
        }
        
        public async Task<object> Parse(object value, TemplateData template = null)
        {
            template ??= (await _components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component?.GetCloudformationData(x.tree))
                    .WhenAll())
                .Merge();
            
            return await CloudformationResources.ParseValue(
                value,
                template, 
                _projectSettings,
                _configuration.Settings.AwsRegion,
                async service =>
                {
                    var services = _configuration.GetConfiguredServices();

                    if (!services.Contains(service))
                        return (false, _configuration.Settings.ServicesPort);

                    var isRunning = await WaitForStartedAndResourcesCreated(TimeSpan.FromMinutes(2));

                    return (isRunning, _configuration.Settings.ServicesPort);
                });
        }

        private async Task<bool> WaitForStart(TimeSpan timeout)
        {
            Console.WriteLine($"Waiting for cloudformation stack {Name} to start.");
            
            var requiredServices = _configuration.GetConfiguredServices();
            
            var timer = Stopwatch.StartNew();

            while (timer.Elapsed < timeout)
            {
                try
                {
                    var response = await HttpClient.GetAsync($"http://localhost:{_configuration.Settings.ServicesPort}/health");

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = Json.DeSerialize<HealthResponse>(await response.Content.ReadAsStringAsync());

                        if (responseData.Services.Any() 
                            && requiredServices.All(x => responseData.Services.ContainsKey(x) && responseData.Services[x] == "running" || responseData.Services[x] == "available"))
                        {
                            Console.WriteLine($"Running services: {string.Join(", ", responseData.Services.Keys)}");
                        
                            return true;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));  
                }
            }

            return false;
        }

        private async Task<bool> WaitForStartedAndResourcesCreated(TimeSpan timeout)
        {
            var timer = Stopwatch.StartNew();
            
            var started = await WaitForStart(timeout);

            if (!started)
                return false;
            
            while (_isStarting && timer.Elapsed < timeout)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            
            return !_isStarting;
        }

        public static async Task<CloudformationStackComponent> Init(
            Components.ComponentTree components, 
            ProjectSettings settings)
        {
            if (!File.Exists(Path.Combine(components.Path.FullName, ConfigFileName)))
                return null;
            
            var deserializer = new Deserializer();
            return new CloudformationStackComponent(
                deserializer.Deserialize<CloudformationStackConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(components.Path.FullName, ConfigFileName))),
                settings,
                components.Path,
                settings,
                components);
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
                        "sts"
                    }.ToImmutableList(),
                    ["cognito"] = new List<string>
                    {
                        "cognito-identity",
                        "cognito-idp"
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
                return settings.GetRootedPath($"./.localstack/{Name}");
            }
            
            public IImmutableList<string> GetConfiguredServices()
            {
                var services = new List<string>();

                foreach (var service in Settings.Services ?? new List<string>())
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
                public IList<string> Services { get; set; }
                public string DeploymentBucketName { get; set; }
                public string DeploymentStackName { get; set; }
                public IList<string> PortMappings { get; set; } = new List<string>();
            }
        }
        
        private class HealthResponse
        {
            public IImmutableDictionary<string, string> Services { get; set; }
        }
    }
}