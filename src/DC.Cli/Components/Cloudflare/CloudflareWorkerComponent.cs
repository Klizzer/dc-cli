using System;
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
        
        public async Task<bool> Test()
        {
            await Restore();
            
            return await _dockerContainer
                .Temporary()
                .Run("test");
        }

        public async Task<bool> Build()
        {
            await Restore();
            
            return await _dockerContainer
                .Temporary()
                .Run("build");
        }
        
        public async Task<bool> Restore()
        {
            return await _dockerContainer
                .Temporary()
                .AsCurrentUser()
                .Run("");
        }

        public async Task<bool> Logs()
        {
            var response = await Docker.Logs(_dockerContainer.Name);

            Console.WriteLine(response);
            
            return true;
        }
        
        public async Task<bool> Start(Components.ComponentTree components)
        {
            var watchResponse = await _watchContainer
                .Detached()
                .Run("watch");

            if (!watchResponse)
                return false;
            
            var startResponse = await _dockerContainer
                .Port(_configuration.Settings.Port, 3000)
                .Detached()
                .WithEmptyVolume("/usr/src/app/node_modules/")
                .Run("start");

            return startResponse;
        }

        public Task<bool> Stop()
        {
            Docker.Stop(_dockerContainer.Name);
            Docker.Remove(_dockerContainer.Name);
            Docker.Stop(_watchContainer.Name);
            Docker.Remove(_watchContainer.Name);

            return Task.FromResult(true);
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