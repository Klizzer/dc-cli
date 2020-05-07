using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class ClientComponent : IComponent
    {
        private const string ConfigFileName = "client.config.yml";
        
        private static readonly IImmutableDictionary<ClientType, Func<DirectoryInfo, Task>> TypeHandlers =
            new Dictionary<ClientType, Func<DirectoryInfo, Task>>
            {
                [ClientType.VueNuxt] = CreateVueNuxt
            }.ToImmutableDictionary();

        private readonly ClientConfiguration _configuration;
        
        private ClientComponent(DirectoryInfo path, ClientConfiguration configuration)
        {
            Path = path;
            _configuration = configuration;
        }

        public string BaseUrl => _configuration.Settings.BaseUrl;
        public int Port => _configuration.Settings.Port;
        public string Name => _configuration.Name;
        public DirectoryInfo Path { get; }

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
                System.IO.Path.Combine(dir.FullName, ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("CLIENT_NAME", dir.Name),
                ("PORT", clientPort.ToString()),
                ("CLIENT_TYPE", clientType.ToString()),
                ("BASE_URL", baseUrl));

            await Templates.Extract(
                "client.make",
                settings.GetRootedPath("services/client.make"),
                Templates.TemplateType.Services,
                false);
            
            await TypeHandlers[clientType](dir);
        }
        
        public Task<BuildResult> Build(IBuildContext context)
        {
            return Task.FromResult(new BuildResult(true, ""));
        }

        public async Task<TestResult> Test()
        {
            if (!File.Exists(System.IO.Path.Combine(Path.FullName, "package.json")))
                return new TestResult(true, "");

            var packageData =
                Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(System.IO.Path.Combine(Path.FullName, "package.json")));

            if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                .ContainsKey("test"))
            {
                return new TestResult(true, "");
            }
            
            var testResult = await Docker
                .CreateContainer("node")
                .EntryPoint("yarn")
                .WithVolume(Path.FullName, "/usr/local/src", true)
                .Run("run test");
                
            return new TestResult(testResult.exitCode == 0, testResult.output);
        }
        
        public static IEnumerable<ClientComponent> FindAtPath(DirectoryInfo path)
        {
            if (!File.Exists(System.IO.Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new ClientComponent(
                path,
                deserializer.Deserialize<ClientConfiguration>(
                    File.ReadAllText(System.IO.Path.Combine(path.FullName, ConfigFileName))));
        }
        
        private static Task CreateVueNuxt(DirectoryInfo dir)
        {
            return Docker
                .CreateContainer("node")
                .Interactive()
                .EntryPoint("yarn")
                .WithVolume(dir.Parent?.FullName ?? "", "/usr/local/src", true)
                .Run($"create nuxt-app {dir.Name}");
        }
        
        private class PackageJsonData
        {
            public IImmutableDictionary<string, string> Scripts { get; set; }
        }
        
        private class ClientConfiguration
        {
            public string Name { get; set; }
            public ClientSettings Settings { get; set; }
            
            public class ClientSettings
            {
                public int Port { get; set; }
                public string Type { get; set; }
                public string BaseUrl { get; set; }
            }
        }
    }
}