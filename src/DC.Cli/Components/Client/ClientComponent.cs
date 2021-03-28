using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Client
{
    public class ClientComponent : IBuildableComponent, 
        ITestableComponent,
        IRestorableComponent,
        ICleanableComponent,
        IStartableComponent,
        IComponentWithLogs,
        ICloudformationComponent
    {
        public const string ConfigFileName = "js-client.config.yml";

        private readonly DirectoryInfo _path;
        private readonly ClientConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;
        
        private ClientComponent(
            DirectoryInfo path,
            ClientConfiguration configuration,
            Docker.Container dockerContainer)
        {
            _path = path;
            _configuration = configuration;
            _dockerContainer = dockerContainer;
        }

        public string Name => _configuration.Name;
        
        public Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations(Components.ComponentTree components)
        {
            return _configuration.Settings.Template.GetRequiredConfigurations();
        }

        public Task<TemplateData> GetCloudformationData(Components.ComponentTree components)
        {
            return Task.FromResult(_configuration.Settings.Template);
        }

        public async Task<bool> Restore()
        {
            if (!File.Exists(Path.Combine(_path.FullName, "package.json")))
                return true;

            return await _dockerContainer
                .Temporary()
                .Run("");
        }
        
        public async Task<bool> Clean()
        {
            if (!File.Exists(Path.Combine(_path.FullName, "package.json")))
                return true;
            
            var packageData =
                Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(_path.FullName, "package.json")));

            if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                .ContainsKey("clean"))
            {
                return true;
            }
            
            return await _dockerContainer
                .Temporary()
                .Run("clean");
        }

        public async Task<bool> Build()
        {
            var restoreResult = await Restore();

            if (!restoreResult)
                return false;
            
            if (!File.Exists(Path.Combine(_path.FullName, "package.json")))
                return true;
            
            var packageData =
                Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(_path.FullName, "package.json")));

            if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                .ContainsKey("build"))
            {
                return true;
            }
            
            return await _dockerContainer
                .Temporary()
                .Run("build");
        }

        public async Task<bool> Test()
        {
            var restoreResult = await Restore();

            if (!restoreResult)
                return false;
            
            if (!File.Exists(Path.Combine(_path.FullName, "package.json")))
                return true;

            var packageData =
                Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(_path.FullName, "package.json")));

            if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                .ContainsKey("test"))
            {
                return true;
            }
            
            return await _dockerContainer
                .Temporary()
                .Run("test");
        }

        public async Task<bool> Start(Components.ComponentTree components)
        {
            var restoreSuccess = await Restore();

            if (!restoreSuccess)
                return false;
            
            var parsers = components
                .FindAll<IParseCloudformationValues>(Components.Direction.Out)
                .Select(x => x.component)
                .ToImmutableList();
            
            var result = new Dictionary<string, string>();

            var amplifyApp =
                _configuration.Settings.Template.Resources.Values.FirstOrDefault(x => x.Type == "AWS::Amplify::App");

            if (amplifyApp != null && amplifyApp.Properties.ContainsKey("EnvironmentVariables"))
            {
                foreach (var environmentVariable in (amplifyApp.Properties["EnvironmentVariables"] as Dictionary<string, object>) ?? new Dictionary<string, object>())
                    result[environmentVariable.Key] = (await parsers.Parse(environmentVariable.Value)) as string;   
            }

            var container = result.Aggregate(
                _dockerContainer, 
                (current, variable) => 
                    current.EnvironmentVariable(variable.Key, variable.Value));

            return await container
                .Detached()
                .Port(_configuration.Settings.Port, 3000)
                .Run("dev --hostname 0.0.0.0");
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
        
        public static async Task<ClientComponent> Init(
            DirectoryInfo path,
            Func<ClientConfiguration, Docker.Container> getBaseContainer)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                return null;
            
            var deserializer = new Deserializer();

            var configuration = deserializer.Deserialize<ClientConfiguration>(
                await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName)));
            
            return new ClientComponent(path, configuration, getBaseContainer(configuration));
        }
        
        private class PackageJsonData
        {
            public IImmutableDictionary<string, string> Scripts { get; set; }
        }
        
        public class ClientConfiguration
        {
            public string Name { get; set; }
            public ClientSettings Settings { get; set; }

            public static string GetContainerName(ProjectSettings settings, string name)
            {
                return $"{settings.GetProjectName()}-client-{name}";
            }
            
            public class ClientSettings
            {
                public int Port { get; set; }
                public string Type { get; set; }
                public TemplateData Template { get; set; }
            }
        }
    }
}