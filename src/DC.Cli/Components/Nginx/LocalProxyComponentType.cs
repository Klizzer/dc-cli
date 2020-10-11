using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli.Components.Nginx
{
    public class LocalProxyComponentType : IComponentType<LocalProxyComponent, LocalProxyComponentType.ComponentData>
    {
        public async Task<LocalProxyComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var configFilePath = Path.Combine(tree.Path.FullName, LocalProxyComponent.ConfigFileName);
            
            if (File.Exists(configFilePath))
                throw new InvalidCastException($"There is already a proxy configured at: {tree.Path.FullName}");

            var proxyPort = data.Port ?? ProjectSettings.GetRandomUnusedPort();

            await Templates.Extract(
                "proxy.conf",
                Path.Combine(tree.Path.FullName, "proxy.nginx.conf"),
                Templates.TemplateType.Config);

            await Templates.Extract(
                LocalProxyComponent.ConfigFileName,
                configFilePath,
                Templates.TemplateType.Infrastructure,
                ("PROXY_NAME", data.Name),
                ("PORT", proxyPort.ToString()));

            return await LocalProxyComponent.Init(tree.Path, settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var component = await LocalProxyComponent.Init(components.Path, settings);

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        public static bool HasProxyAt(string path)
        {
            return File.Exists(Path.Combine(path, LocalProxyComponent.ConfigFileName));
        }

        public static bool HasProxyPathFor(string path, int port)
        {
            return HasProxyAt(path) && File.Exists(Path.Combine(path, $"_paths/{port}-path.conf"));
        }
        
        public static async Task AddProxyPath(ProjectSettings settings, string path, string baseUrl, int port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (!File.Exists(Path.Combine(dir.FullName, LocalProxyComponent.ConfigFileName)))
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
        
        public class ComponentData
        {
            public ComponentData(string name, int? port)
            {
                Name = name;
                Port = port;
            }

            public string Name { get; }
            public int? Port { get; }
        }
    }
}