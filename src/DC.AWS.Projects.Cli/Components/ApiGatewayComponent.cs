using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class ApiGatewayComponent : ICloudformationComponent, IStartableComponent, ISupplyLogs, IHaveHttpEndpoint
    {
        private const string ConfigFileName = "api-gw.config.yml";

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
                    configuration.GetContainerImageName(),
                    configuration.GetContainerName())
                .WithDockerSocket()
                .WithVolume(path.FullName, $"/usr/src/app/${settings.GetRelativePath(path.FullName)}")
                .WithVolume(System.IO.Path.Combine(_tempPath.FullName, "environment.json"), "/usr/src/app/environment.json")
                .WithVolume(System.IO.Path.Combine(_tempPath.FullName, "template.yml"), "/usr/src/app/template.yml")
                .Port(configuration.Settings.Port, 3000);
        }

        public string BaseUrl => _configuration.Settings.BaseUrl;
        public int Port => _configuration.Settings.Port;
        public string Name => _configuration.Name;
        public DirectoryInfo Path { get; }
        
        public static async Task InitAt(
            ProjectSettings settings,
            string path,
            string baseUrl,
            string language,
            int? port = null)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));

            if (Directory.Exists(settings.GetRootedPath(dir.FullName)))
                throw new InvalidOperationException($"You can't add a new api at: \"{dir.FullName}\". It already exists.");

            dir.Create();

            var apiPort = port ?? ProjectSettings.GetRandomUnusedPort();
            
            await Templates.Extract(
                ConfigFileName,
                System.IO.Path.Combine(dir.FullName, ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("API_NAME", dir.Name),
                ("PORT", apiPort.ToString()),
                ("DEFAULT_LANGUAGE", language),
                ("BASE_URL", baseUrl));
        }

        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            if (_tempPath.Exists)
                _tempPath.Delete(true);
                
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
                .Detached()
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
            return FunctionLanguage.Parse(_configuration.Settings.DefaultLanguage) ?? settings.GetDefaultLanguage();
        }

        public string GetUrl(string path)
        {
            var url = _configuration.Settings.BaseUrl;
            
            url = url.Trim().TrimStart('/').TrimEnd('/');

            path = path.Trim().TrimStart('/');

            return $"/{url}/{path}";
        }

        public static IEnumerable<ApiGatewayComponent> FindAtPath(DirectoryInfo path, ProjectSettings settings)
        {
            if (!File.Exists(System.IO.Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new ApiGatewayComponent(
                path,
                deserializer.Deserialize<ApiConfiguration>(
                    File.ReadAllText(System.IO.Path.Combine(path.FullName, ConfigFileName))),
                settings);
        }
        
        private class ApiConfiguration
        {
            public string Name { get; set; }
            public ApiSettings Settings { get; set; }

            public string GetContainerImageName()
            {
                //TODO:Use better name
                return Name;
            }

            public string GetContainerName()
            {
                //TODO: Use better name
                return Name;
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