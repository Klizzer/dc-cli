using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
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

        private CloudformationStackComponent(
            CloudformationStackConfiguration configuration,
            ProjectSettings settings,
            DirectoryInfo path, 
            ProjectSettings projectSettings)
        {
            _configuration = configuration;
            _path = path;
            _projectSettings = projectSettings;
            _tempDir = new DirectoryInfo(Path.Combine(path.FullName, ".tmp"));

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
                .EnvironmentVariable("SERVICES", string.Join(",", (configuration.Settings.Services ?? new List<string>())))
                .EnvironmentVariable("DATA_DIR", "/tmp/localstack/data")
                .EnvironmentVariable("LAMBDA_REMOTE_DOCKER", "0")
                .EnvironmentVariable("DEBUG", "1")
                .EnvironmentVariable("LOCALSTACK_API_KEY", projectSettings.GetConfiguration("localstackApiKey"));
        }

        public string Name => _configuration.Name;
        
        public Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations()
        {
            return Task.FromResult<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>>(
                new List<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>
            {
                ("localstackApiKey", "Enter your localstack api key if you have any:",
                    INeedConfiguration.ConfigurationType.User)
            });
        }

        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            var startedServices = _configuration.GetConfiguredServices();
            
            if (!startedServices.Any())
                return new ComponentActionResult(true, "");
            
            await Stop();
            
            var startResult = await _dockerContainer.Run("");
            
            if (startResult.exitCode != 0)
                return new ComponentActionResult(false, startResult.output);

            var started = await WaitForStart(TimeSpan.FromMinutes(1));
            
            if (!started)
                return new ComponentActionResult(false, "Can't start localstack within 60 seconds.");
            
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component.GetCloudformationData())
                    .WhenAll())
                .Merge();

            await CloudformationResources.EnsureResourcesExist(template, _configuration, _projectSettings);

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
            if (_tempDir.Exists)
                _tempDir.Delete(true);
            
            _tempDir.Create();
            
            var outputDir = new DirectoryInfo(Path.Combine(_tempDir.FullName, "output"));
            
            outputDir.Create();
            
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component.GetCloudformationData())
                    .WhenAll())
                .Merge();
            
            var serializer = new Serializer();

            await File.WriteAllTextAsync(
                Path.Combine(_tempDir.FullName, "template.yml"),
                serializer.Serialize(template));

            var cliDocker = Docker.TemporaryContainerFromImage("amazon/aws-cli")
                .WithVolume(Path.Combine(User.GetHome(), ".aws"), "/root/.aws")
                .WithVolume(
                    _projectSettings.GetRootedPath(_path.FullName),
                    $"/usr/src/app/{_projectSettings.GetRelativePath(_path.FullName)}")
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
                .Run($"cloudformation package --template-file ./template.yml --output-template-file ./output/template.yml --s3-bucket {_configuration.Settings.DeploymentBucketName} --s3-prefix {version}");
            
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
        
        public Task<object> Parse(object value, TemplateData template)
        {
            return CloudformationResources.ParseValue(
                value,
                template, 
                _projectSettings,
                async service =>
                {
                    var services = _configuration.GetConfiguredServices();

                    if (!service.Contains(service))
                        return (false, _configuration.Settings.ServicesPort);

                    var isRunning = await WaitForStart(TimeSpan.FromMinutes(1));

                    return (isRunning, _configuration.Settings.ServicesPort);
                });
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
            }
        }
        
        private class HealthResponse
        {
            public IImmutableDictionary<string, string> Services { get; set; }
        }
    }
}