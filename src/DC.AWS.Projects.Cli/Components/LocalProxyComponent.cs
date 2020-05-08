using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class LocalProxyComponent : IStartableComponent, ISupplyLogs
    {
        private const string ConfigFileName = "proxy.config.yml";

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

        public static bool HasProxyAt(string path)
        {
            return File.Exists(Path.Combine(path, ConfigFileName));
        }

        public static bool HasProxyPathFor(string path, int port)
        {
            return HasProxyAt(path) && File.Exists(Path.Combine(path, $"_paths/{port}-path.conf"));
        }
        
        public static async Task InitAt(ProjectSettings settings, string path, int? port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (File.Exists(Path.Combine(dir.FullName, ConfigFileName)))
                throw new InvalidCastException($"There is already a proxy configured at: {dir.FullName}");
            
            if (!dir.Exists)
                dir.Create();

            var proxyPort = port ?? ProjectSettings.GetRandomUnusedPort();

            await Templates.Extract(
                "proxy.conf",
                Path.Combine(dir.FullName, "proxy.nginx.conf"),
                Templates.TemplateType.Config);

            await Templates.Extract(
                ConfigFileName,
                Path.Combine(dir.FullName, ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("PROXY_NAME", dir.Name),
                ("PORT", proxyPort.ToString()));
        }

        public static async Task AddProxyPath(ProjectSettings settings, string path, string baseUrl, int port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (!File.Exists(Path.Combine(dir.FullName, ConfigFileName)))
                throw new InvalidCastException($"There is no proxy configured at: {dir.FullName}");

            var pathsPath = new DirectoryInfo(Path.Combine(dir.FullName, "_paths"));
            
            if (!pathsPath.Exists)
                pathsPath.Create();

            await Templates.Extract(
                "proxy-path.conf",
                Path.Combine(pathsPath.FullName, $"{port}-path.conf"),
                Templates.TemplateType.Config,
                ("BASE_URL", (baseUrl ?? "").TrimStart('/')),
                ("PORT", port.ToString()));
        }

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

        public static IEnumerable<LocalProxyComponent> FindAtPath(DirectoryInfo path, ProjectSettings settings)
        {
            if (!HasProxyAt(path.FullName)) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new LocalProxyComponent(
                path,
                deserializer.Deserialize<ProxyConfiguration>(
                    File.ReadAllText(Path.Combine(path.FullName, ConfigFileName))),
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