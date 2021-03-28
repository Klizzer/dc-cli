using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.ApiGateway
{
    public class ApiGatewayComponent : ICloudformationComponent, IStartableComponent, IComponentWithLogs
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
                .ContainerFromProject(
                    "sam",
                    configuration.GetContainerImageName(settings),
                    configuration.GetContainerName(settings),
                    false)
                .WithDockerSocket()
                .Detached()
                .WithVolume(path.FullName, $"/usr/src/app/{settings.GetRelativePath(path.FullName)}")
                .WithVolume(System.IO.Path.Combine(_tempPath.FullName, "environment.json"), "/usr/src/app/environment.json")
                .WithVolume(System.IO.Path.Combine(_tempPath.FullName, "template.yml"), "/usr/src/app/template.yml")
                .Port(configuration.Settings.Port, 3000);
        }

        public string Name => _configuration.Name;

        public DirectoryInfo Path { get; }
        
        public Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations(Components.ComponentTree components)
        {
            return _configuration.Settings.Template.GetRequiredConfigurations();
        }
        
        public async Task<bool> Start(Components.ComponentTree components)
        {
            _tempPath.Create();
            
            var template = (await components
                    .FindAll<ICloudformationComponent>(Components.Direction.In)
                    .Select(x => x.component.GetCloudformationData(x.tree))
                    .WhenAll())
                .Merge();
            
            await File.WriteAllTextAsync(System.IO.Path.Combine(_tempPath.FullName, "environment.json"),
                JsonConvert.SerializeObject(
                    await template.FindEnvironmentVariables(components)));

            var serializer = new Serializer();

            await File.WriteAllTextAsync(
                System.IO.Path.Combine(_tempPath.FullName, "template.yml"),
                serializer.Serialize(template));
            
            return await _dockerContainer
                .WithVolume(Path.FullName, Path.FullName)
                .Run($"local start-api --env-vars ./environment.json --docker-volume-basedir \"{_settings.ProjectRoot}\" --host 0.0.0.0");
        }

        public Task<bool> Stop()
        {
            Docker.Remove(_dockerContainer.Name);
            
            if (_tempPath.Exists)
                _tempPath.Delete(true);

            return Task.FromResult(true);
        }

        public async Task<bool> Logs()
        {
            var result = await Docker.Logs(_dockerContainer.Name);
            
            Console.WriteLine(result);

            return true;
        }

        public Task<TemplateData> GetCloudformationData(Components.ComponentTree components)
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