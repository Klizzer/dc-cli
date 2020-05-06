using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public class LocalProxyComponent : IComponent
    {
        private readonly ProxyType _proxyType;
        
        private LocalProxyComponent(DirectoryInfo path, ProxyType proxyType)
        {
            Path = path;
            _proxyType = proxyType;
        }

        public DirectoryInfo Path { get; }

        public static Task AddChildTo(
            ProjectSettings settings,
            string path,
            string baseUrl,
            string upstreamName)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            var url = baseUrl.MakeRelativeUrl();
                
            return Templates.Extract(
                "client-proxy.conf",
                System.IO.Path.Combine(dir.FullName, $"_child_paths/{upstreamName}.conf"),
                Templates.TemplateType.Config,
                ("BASE_URL", url),
                ("UPSTREAM_NAME", upstreamName));
        }

        public static async Task<LocalProxyComponent> InitAt(
            ProjectSettings settings,
            string path,
            string baseUrl,
            ProxyType proxyType,
            int port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (!dir.Exists)
                dir.Create();
            
            var url = baseUrl.MakeRelativeUrl();

            await Templates.Extract(
                "proxy.conf",
                System.IO.Path.Combine(dir.FullName, "proxy.nginx.conf"),
                Templates.TemplateType.Config,
                ("BASE_URL", url),
                ("UPSTREAM_NAME", $"{dir.Name}-{proxyType.ToString().ToLower()}-upstream"));
            
            var proxyPath = System.IO.Path.Combine(settings.ProjectRoot, $"services/{dir.Name}.proxy.make");

            if (!File.Exists(proxyPath))
            {
                await Templates.Extract(
                    "proxy.make",
                    proxyPath,
                    Templates.TemplateType.Services,
                    ("PROXY_NAME", dir.Name),
                    ("CONFIG_PATH", settings.GetRelativePath(dir.FullName)),
                    ("PORT", port.ToString()));
            }
            
            return new LocalProxyComponent(dir, proxyType);
        }
        
        public async Task<BuildResult> Build(IBuildContext context)
        {
            var configDestination =
                System.IO.Path.Combine(context.ProjectSettings.ProjectRoot, "config/.generated/proxy-upstreams");

            if (!Directory.Exists(configDestination))
                Directory.CreateDirectory(configDestination);
            
            switch (_proxyType)
            {
                case ProxyType.Api:
                    var api = context.ProjectSettings.Apis[Path.Name];
                    
                    await Templates.Extract(
                        "proxy-upstream.conf",
                        System.IO.Path.Combine(configDestination, $"{Path.Name}-api-upstream.conf"),
                        Templates.TemplateType.Config,
                        ("NAME", $"{Path.Name}-api-upstream"),
                        ("LOCAL_IP", RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ProjectSettings.GetLocalIpAddress() : "host.docker.internal"),
                        ("UPSTREAM_PORT", api.Port.ToString()));
                    
                    break;
                case ProxyType.Client:
                    var client = context.ProjectSettings.Clients[Path.Name];

                    await Templates.Extract(
                        "proxy-upstream.conf",
                        System.IO.Path.Combine(configDestination, $"{Path.Name}-client-upstream.conf"),
                        Templates.TemplateType.Config,
                        ("NAME", $"{Path.Name}-client-upstream"),
                        ("LOCAL_IP",
                            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                                ? ProjectSettings.GetLocalIpAddress()
                                : "host.docker.internal"),
                        ("UPSTREAM_PORT", client.Port.ToString()));
                    break;
            }

            return new BuildResult(true, "");
        }

        public Task<TestResult> Test()
        {
            return Task.FromResult(new TestResult(true, ""));
        }
        
        public static IEnumerable<LocalProxyComponent> FindAtPath(DirectoryInfo path)
        {
            if (File.Exists(System.IO.Path.Combine(path.FullName, "api.infra.yml")))
                yield return new LocalProxyComponent(path, ProxyType.Api);
            
            if (File.Exists(System.IO.Path.Combine(path.FullName, "client.infra.yml")))
                yield return new LocalProxyComponent(path, ProxyType.Client);
        }
        
        public enum ProxyType
        {
            Api,
            Client
        }
    }
}