using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class ClientComponent : IBuildableComponent, 
        ITestableComponent,
        IRestorableComponent,
        IStartableComponent,
        ISupplyLogs
    {
        private const string ConfigFileName = "client.config.yml";
        
        private static readonly IImmutableDictionary<ClientType, Func<DirectoryInfo, ClientConfiguration, Task>> TypeHandlers =
            new Dictionary<ClientType, Func<DirectoryInfo, ClientConfiguration, Task>>
            {
                [ClientType.VueNuxt] = CreateVueNuxt
            }.ToImmutableDictionary();

        private readonly DirectoryInfo _path;
        private readonly ClientConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;
        
        private ClientComponent(DirectoryInfo path, ClientConfiguration configuration)
        {
            _path = path;
            _configuration = configuration;

            _dockerContainer = CreateBaseContainer(path, configuration);
        }

        public string BaseUrl => _configuration.Settings.BaseUrl;
        public int Port => _configuration.Settings.Port;
        public string Name => _configuration.Name;

        public static async Task InitAt(ProjectSettings settings,
            string path,
            string baseUrl,
            ClientType clientType,
            int? port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (dir.Exists)
                throw new InvalidOperationException($"You can't create a client at \"{dir.FullName}\". It already exists.");
            
            dir.Create();

            var clientPort = port ?? ProjectSettings.GetRandomUnusedPort();

            await Templates.Extract(
                ConfigFileName,
                Path.Combine(dir.FullName, ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("CLIENT_NAME", dir.Name),
                ("PORT", clientPort.ToString()),
                ("CLIENT_TYPE", clientType.ToString()),
                ("BASE_URL", baseUrl));
            
            var deserializer = new Deserializer();
            var configuration = deserializer.Deserialize<ClientConfiguration>(
                await File.ReadAllTextAsync(Path.Combine(dir.FullName, ConfigFileName)));
            
            await TypeHandlers[clientType](dir, configuration);
        }

        public async Task<ComponentActionResult> Restore()
        {
            if (!File.Exists(Path.Combine(_path.FullName, "package.json")))
                return new ComponentActionResult(true, "");
            
            var result = await _dockerContainer
                .Temporary()
                .Run("");

            return new ComponentActionResult(result.exitCode == 0, result.output);
        }

        public Task<ComponentActionResult> Build()
        {
            //TODO: Build in prod
            return Task.FromResult(new ComponentActionResult(true, ""));
        }

        public async Task<ComponentActionResult> Test()
        {
            if (!File.Exists(Path.Combine(_path.FullName, "package.json")))
                return new ComponentActionResult(true, "");

            var packageData =
                Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(_path.FullName, "package.json")));

            if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                .ContainsKey("test"))
            {
                return new ComponentActionResult(true, "");
            }
            
            var testResult = await _dockerContainer
                .Temporary()
                .Run("run test");
                
            return new ComponentActionResult(testResult.exitCode == 0, testResult.output);
        }

        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            var result = await _dockerContainer
                .Detached()
                .Run("run dev --hostname 0.0.0.0");

            return new ComponentActionResult(result.exitCode == 0, result.output);
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

        public static IEnumerable<ClientComponent> FindAtPath(DirectoryInfo path)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new ClientComponent(
                path,
                deserializer.Deserialize<ClientConfiguration>(
                    File.ReadAllText(Path.Combine(path.FullName, ConfigFileName))));
        }
        
        private static Task CreateVueNuxt(DirectoryInfo dir, ClientConfiguration configuration)
        {
            return CreateBaseContainer(dir.Parent, configuration)
                .Interactive()
                .Run($"create nuxt-app {dir.Name}");
        }

        private static Docker.Container CreateBaseContainer(FileSystemInfo path, ClientConfiguration configuration)
        {
            return Docker
                .ContainerFromImage("node", configuration.GetContainerName())
                .EntryPoint("yarn")
                .Port(configuration.Settings.Port, 3000)
                .WithVolume(path.FullName, "/usr/local/src", true);
        }
        
        private class PackageJsonData
        {
            public IImmutableDictionary<string, string> Scripts { get; set; }
        }
        
        private class ClientConfiguration
        {
            public string Name { get; set; }
            public ClientSettings Settings { get; set; }

            public string GetContainerName()
            {
                return Name;
            }
            
            public class ClientSettings
            {
                public int Port { get; set; }
                public string Type { get; set; }
                public string BaseUrl { get; set; }
            }
        }
    }
}