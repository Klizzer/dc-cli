using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Cloudflare
{
    public class CloudflareWorkerComponent : 
        IStartableComponent,
        IComponentWithLogs,
        IBuildableComponent,
        ITestableComponent,
        IRestorableComponent,
        IHavePackageResources
    {
        public const string ConfigFileName = "cloudflare-worker.config.yml";
        
        private readonly CloudflareWorkerConfiguration _configuration;
        private readonly DirectoryInfo _path;
        private readonly Docker.Container _dockerContainer;
        private readonly Docker.Container _watchContainer;
        private readonly ProjectSettings _settings;

        private CloudflareWorkerComponent(
            CloudflareWorkerConfiguration configuration,
            DirectoryInfo path,
            ProjectSettings settings)
        {
            _configuration = configuration;
            _path = path;
            _settings = settings;

            _dockerContainer = Docker
                .ContainerFromImage("node", $"{settings.GetProjectName()}-cf-{configuration.Name}")
                .EntryPoint("yarn")
                .WithVolume(path.FullName, "/usr/src/app", true)
                .EnvironmentVariable("DESTINATION_PORT", configuration.Settings.DestinationPort.ToString());
            
            _watchContainer = _dockerContainer
                .WithName($"{_dockerContainer.Name}-watcher")
                .WithEmptyVolume("/usr/src/app/node_modules/");
        }

        public string Name => _configuration.Name;
        
        public async Task<ComponentActionResult> Test()
        {
            await Restore();
            
            var response = await _dockerContainer
                .Temporary()
                .Run("test");

            return new ComponentActionResult(response.exitCode == 0, response.output);
        }

        public async Task<ComponentActionResult> Build()
        {
            await Restore();
            
            var response = await _dockerContainer
                .Temporary()
                .AsCurrentUser()
                .Run("build");

            return new ComponentActionResult(response.exitCode == 0, response.output);
        }
        
        public async Task<ComponentActionResult> Restore()
        {
            var response = await _dockerContainer
                .Temporary()
                .AsCurrentUser()
                .Run("");

            return new ComponentActionResult(response.exitCode == 0, response.output);
        }

        public async Task<ComponentActionResult> Logs()
        {
            var response = await Docker.Logs(_dockerContainer.Name);

            return new ComponentActionResult(true, response);
        }
        
        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            await Stop();
            
            var watchResponse = await _watchContainer
                .Detached()
                .Run("watch");
            
            var startResponse = await _dockerContainer
                .Port(_configuration.Settings.Port, 3000)
                .Detached()
                .WithEmptyVolume("/usr/src/app/node_modules/")
                .Run("start");
            
            return new ComponentActionResult(
                startResponse.exitCode == 0 && watchResponse.exitCode == 0, 
                $"{watchResponse.output}\n{startResponse.output}");
        }

        public Task<ComponentActionResult> Stop()
        {
            Docker.Stop(_dockerContainer.Name);
            Docker.Remove(_dockerContainer.Name);
            Docker.Stop(_watchContainer.Name);
            Docker.Remove(_watchContainer.Name);

            return Task.FromResult(new ComponentActionResult(true, ""));
        }
        
        public async Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components,
            string version)
        {
            await Build();
            
            var result = new List<PackageResource>();
            
            var distDirectory = new DirectoryInfo(Path.Combine(_path.FullName, "dist"));

            foreach (var file in distDirectory.EnumerateFiles())
            {
                result.Add(new PackageResource(
                    _settings.GetRelativePath(file.FullName, components.Path.FullName),
                    await File.ReadAllBytesAsync(file.FullName)));
            }

            return result.ToImmutableList();
        }

        public static async Task<CloudflareWorkerComponent> Init(DirectoryInfo path, ProjectSettings settings)
        {
            var filePath = Path.Combine(path.FullName, ConfigFileName);

            if (!File.Exists(filePath))
                return null;
            
            var deserializer = new Deserializer();

            var configuration = deserializer.Deserialize<CloudflareWorkerConfiguration>(
                await File.ReadAllTextAsync(filePath));

            return new CloudflareWorkerComponent(configuration, path, settings);
        }
        
        public class CloudflareWorkerConfiguration
        {
            public string Name { get; set; }
            public CloudflareWorkerSettings Settings { get; set; }
            
            public class CloudflareWorkerSettings
            {
                public int Port { get; set; }
                public int DestinationPort { get; set; }
            }
        }
    }
}