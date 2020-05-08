using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components.Aws.ApiGateway
{
    public class ApiGatewayComponent : ICloudformationComponent, IStartableComponent, IComponentWithLogs, IHaveHttpEndpoint
    {
        public const string ConfigFileName = "api-gw.config.yml";

        private readonly ApiConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;
        private readonly DirectoryInfo _tempPath;
        private readonly ProjectSettings _settings;
        
        private ApiGatewayComponent(DirectoryInfo path, ApiConfiguration configuration, ProjectSettings settings)
        {
            Path = path;
            _configuration = configuration;
            _settings = settings;
            _tempPath = new DirectoryInfo(System.IO.Path.Combine(path.FullName, ".tmp"));
            
            _dockerContainer = Docker
                .ContainerFromFile(
                    "sam",
                    configuration.GetContainerImageName(settings),
                    configuration.GetContainerName(settings))
                .WithDockerSocket()
                .Detached()
                .WithVolume(path.FullName, $"/usr/src/app/{settings.GetRelativePath(path.FullName)}")
                .WithVolume(System.IO.Path.Combine(_tempPath.FullName, "environment.json"), "/usr/src/app/environment.json")
                .WithVolume(System.IO.Path.Combine(_tempPath.FullName, "template.yml"), "/usr/src/app/template.yml")
                .Port(configuration.Settings.Port, 3000);
        }

        public string BaseUrl => _configuration.Settings.BaseUrl;
        public int Port => _configuration.Settings.Port;
        public string Name => _configuration.Name;
        public DirectoryInfo Path { get; }
        
        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            await Stop();
                
            _tempPath.Create();

            var variableValuesFile = _settings.GetRootedPath(".env/variables.json");

            var currentVariables = File.Exists(variableValuesFile)
                ? JsonConvert.DeserializeObject<IImmutableDictionary<string, string>>(
                    await File.ReadAllTextAsync(variableValuesFile))
                : new Dictionary<string, string>().ToImmutableDictionary();
            
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component.GetCloudformationData())
                    .WhenAll())
                .Merge();
            
            var newVariables = new ConcurrentDictionary<string, string>();
            var resourceEnvironmentVariables = template.FindEnvironmentVariables(
                currentVariables,
                (question, name) =>
                {
                    var value = ConsoleInput.Ask(question);

                    newVariables[name] = value;

                    return value;
                }
            );

            await File.WriteAllTextAsync(System.IO.Path.Combine(_tempPath.FullName, "environment.json"),
                JsonConvert.SerializeObject(resourceEnvironmentVariables));

            var configuredVariables = currentVariables.ToDictionary(x => x.Key, x => x.Value);

            foreach (var newVariable in newVariables)
                configuredVariables[newVariable.Key] = newVariable.Value;

            if (!Directory.Exists(_settings.GetRootedPath(".env")))
                Directory.CreateDirectory(_settings.GetRootedPath(".env"));

            await File.WriteAllTextAsync(variableValuesFile, JsonConvert.SerializeObject(configuredVariables));

            var serializer = new Serializer();

            await File.WriteAllTextAsync(
                System.IO.Path.Combine(_tempPath.FullName, "template.yml"),
                serializer.Serialize(template));
            
            var result = await _dockerContainer
                .Run($"local start-api --env-vars ./environment.json --docker-volume-basedir \"{Path.FullName}\" --host 0.0.0.0");

            return new ComponentActionResult(result.exitCode == 0, result.output);
        }

        public Task<ComponentActionResult> Stop()
        {
            Docker.Stop(_dockerContainer.Name);
            Docker.Remove(_dockerContainer.Name);
            
            if (_tempPath.Exists)
                _tempPath.Delete(true);

            return Task.FromResult(new ComponentActionResult(true, ""));
        }

        public async Task<ComponentActionResult> Logs()
        {
            var result = await Docker.Logs(_dockerContainer.Name);

            return new ComponentActionResult(true, result);
        }

        public Task<TemplateData> GetCloudformationData()
        {
            return Task.FromResult(_configuration.Settings.Template);
        }

        public ILanguageVersion GetDefaultLanguage(ProjectSettings settings)
        {
            return FunctionLanguage.GetLanguage(_configuration.Settings.DefaultLanguage,
                settings.GetConfiguration(FunctionLanguage.DefaultLanguageConfigurationKay));
        }

        public string GetUrl(string path)
        {
            var url = _configuration.Settings.BaseUrl;
            
            url = url.Trim().TrimStart('/').TrimEnd('/');

            path = path.Trim().TrimStart('/');

            return $"/{url}/{path}";
        }

        public static async Task<ApiGatewayComponent> Init(DirectoryInfo path, ProjectSettings settings)
        {
            if (!File.Exists(System.IO.Path.Combine(path.FullName, ConfigFileName))) 
                return null;
            
            var deserializer = new Deserializer();
            return new ApiGatewayComponent(
                path,
                deserializer.Deserialize<ApiConfiguration>(
                    await File.ReadAllTextAsync(System.IO.Path.Combine(path.FullName, ConfigFileName))),
                settings);
        }
        
        public class ApiConfiguration
        {
            public string Name { get; set; }
            public ApiSettings Settings { get; set; }

            public string GetContainerImageName(ProjectSettings settings)
            {
                return $"{settings.GetProjectName()}/api-{Name}";
            }

            public string GetContainerName(ProjectSettings settings)
            {
                return $"{settings.GetProjectName()}-api-{Name}";
            }
            
            public class ApiSettings
            {
                public int Port { get; set; }
                public string DefaultLanguage { get; set; }
                public string BaseUrl { get; set; }
                public TemplateData Template { get; set; }
            }
        }
    }
}