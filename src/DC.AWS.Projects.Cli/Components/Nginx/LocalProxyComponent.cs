using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components.Nginx
{
    public class LocalProxyComponent : IStartableComponent, IComponentWithLogs
    {
        public const string ConfigFileName = "proxy.config.yml";

        private readonly ProxyConfiguration _configuration;
        private readonly Docker.Container _dockerContainer;

        private LocalProxyComponent(FileSystemInfo path, ProxyConfiguration configuration, ProjectSettings settings)
        {
            _configuration = configuration;

            _dockerContainer = Docker
                .ContainerFromImage("nginx", configuration.GetContainerName(settings))
                .Detached()
                .Port(configuration.Settings.Port, 80)
                .WithVolume(Path.Combine(path.FullName, "proxy.nginx.conf"), "/etc/nginx/nginx.conf")
                .WithVolume(Path.Combine(path.FullName, "_paths"), "/etc/nginx/_paths");
        }

        public string Name => _configuration.Name;

        public async Task<ComponentActionResult> Start(Components.ComponentTree components)
        {
            var response = await _dockerContainer.Run("");

            return new ComponentActionResult(response.exitCode == 0, response.output);
        }

        public Task<ComponentActionResult> Stop()
        {
            Docker.Stop(_dockerContainer.Name);
            Docker.Remove(_dockerContainer.Name);

            return Task.FromResult(new ComponentActionResult(true, $"Proxy \"{_configuration.Name}\" stopped"));
        }

        public async Task<ComponentActionResult> Logs()
        {
            var result = await Docker.Logs(_dockerContainer.Name);

            return new ComponentActionResult(true, result);
        }

        public static async Task<LocalProxyComponent> Init(DirectoryInfo path, ProjectSettings settings)
        {
            if (!LocalProxyComponentType.HasProxyAt(path.FullName))
                return null;
            
            var deserializer = new Deserializer();
            return new LocalProxyComponent(
                path,
                deserializer.Deserialize<ProxyConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName))),
                settings);
        }
        
        private class ProxyConfiguration
        {
            public string Name { get; set; }
            public ProxySettings Settings { get; set; }
            
            public string GetContainerName(ProjectSettings settings)
            {
                return $"{settings.GetProjectName()}-proxy-{Name}";
            }
            
            public class ProxySettings
            {
                public int Port { get; set; }
            }
        }
    }
}