using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components.Cloudflare
{
    public class CloudflareWorkerComponent : 
        IStartableComponent,
        IComponentWithLogs,
        IBuildableComponent,
        ITestableComponent,
        IRestorableComponent
    {
        public const string ConfigFileName = "cloudflare-worker.config.yml";
        
        private readonly CloudflareWorkerConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;

        private CloudflareWorkerComponent(
            CloudflareWorkerConfiguration configuration,
            DirectoryInfo path,
            ProjectSettings settings)
        {
            _configuration = configuration;

            _dockerContainer = Docker
                .ContainerFromImage("node", $"{settings.GetProjectName()}-cf-{configuration.Name}")
                .Port(configuration.Settings.Port, 3000)
                .WithVolume(path.FullName, "/usr/src/app", true)
                .EnvironmentVariable("DESTINATION_PORT", configuration.Settings.DestinationPort.ToString());
        }

        public string Name => _configuration.Name;
        
        public async Task<ComponentActionResult> Restore()
        {
            var response = await _dockerContainer
                .Temporary()
                .Run("yarn");

            return new ComponentActionResult(response.exitCode == 0, response.output);
        }

        public async Task<ComponentActionResult> Test()
        {
            var response = await _dockerContainer
                .Temporary()
                .Run("yarn run test");

            return new ComponentActionResult(response.exitCode == 0, response.output);
        }

        public async Task<ComponentActionResult> Build()
        {
            var response = await _dockerContainer
                .Temporary()
                .Run("yarn run build");

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
            
            var response = await _dockerContainer
                .Detached()
                .Run("start");
            
            return new ComponentActionResult(response.exitCode == 0, response.output);
        }

        public Task<ComponentActionResult> Stop()
        {
            Docker.Stop(_dockerContainer.Name);
            Docker.Remove(_dockerContainer.Name);

            return Task.FromResult(new ComponentActionResult(true, ""));
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