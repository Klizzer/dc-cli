using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class LocalProxyComponent : IComponent
    {
        private const string ConfigFileName = "proxy.config.yml";

        private readonly ProxyConfiguration _configuration;

        private LocalProxyComponent(DirectoryInfo path, ProxyConfiguration configuration)
        {
            Path = path;
            _configuration = configuration;
        }

        public string Name => _configuration.Name;
        public DirectoryInfo Path { get; }

        public static bool HasProxyAt(string path)
        {
            return File.Exists(System.IO.Path.Combine(path, ConfigFileName));
        }

        public static bool HasProxyPathFor(string path, int port)
        {
            return HasProxyAt(path) && File.Exists(System.IO.Path.Combine(path, $"_paths/{port}-path.conf"));
        }
        
        public static async Task InitAt(ProjectSettings settings, string path, int? port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (File.Exists(System.IO.Path.Combine(dir.FullName, ConfigFileName)))
                throw new InvalidCastException($"There is already a proxy configured at: {dir.FullName}");
            
            if (!dir.Exists)
                dir.Create();

            var proxyPort = port ?? ProjectSettings.GetRandomUnusedPort();

            await Templates.Extract(
                "proxy.conf",
                System.IO.Path.Combine(dir.FullName, "proxy.nginx.conf"),
                Templates.TemplateType.Config);

            await Templates.Extract(
                ConfigFileName,
                System.IO.Path.Combine(dir.FullName, ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("PROXY_NAME", dir.Name),
                ("PORT", proxyPort.ToString()));

            await Templates.Extract(
                "proxy.make",
                settings.GetRootedPath("services/proxy.make"),
                Templates.TemplateType.Services,
                false);
        }

        public static async Task AddProxyPath(ProjectSettings settings, string path, string baseUrl, int port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (!File.Exists(System.IO.Path.Combine(dir.FullName, ConfigFileName)))
                throw new InvalidCastException($"There is no proxy configured at: {dir.FullName}");

            var pathsPath = new DirectoryInfo(System.IO.Path.Combine(dir.FullName, "_paths"));
            
            if (!pathsPath.Exists)
                pathsPath.Create();

            await Templates.Extract(
                "proxy-path.conf",
                System.IO.Path.Combine(pathsPath.FullName, $"{port}-path.conf"),
                Templates.TemplateType.Config,
                ("BASE_URL", (baseUrl ?? "").TrimStart('/')),
                ("PORT", port.ToString()));
        }

        public Task<RestoreResult> Restore()
        {
            return Task.FromResult(new RestoreResult(true, ""));
        }

        public Task<BuildResult> Build(IBuildContext context)
        {
            return Task.FromResult(new BuildResult(true, ""));
        }

        public Task<TestResult> Test()
        {
            return Task.FromResult(new TestResult(true, ""));
        }
        
        public static IEnumerable<LocalProxyComponent> FindAtPath(DirectoryInfo path)
        {
            if (!HasProxyAt(path.FullName)) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new LocalProxyComponent(
                path,
                deserializer.Deserialize<ProxyConfiguration>(
                    File.ReadAllText(System.IO.Path.Combine(path.FullName, ConfigFileName))));
        }
        
        private class ProxyConfiguration
        {
            public string Name { get; set; }
            public ProxySettings Settings { get; set; }
            
            public class ProxySettings
            {
                public int Port { get; set; }
            }
        }
    }
}