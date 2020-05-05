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
        
        public Task<BuildResult> Build(IBuildContext context)
        {
            var configDestination =
                System.IO.Path.Combine(context.ProjectSettings.ProjectRoot, "config/.generated/proxy-upstreams");

            if (!Directory.Exists(configDestination))
                Directory.CreateDirectory(configDestination);
            
            switch (_proxyType)
            {
                case ProxyType.Api:
                    var api = context.ProjectSettings.Apis[Path.Name];
                    
                    Templates.Extract(
                        "proxy-upstream.conf",
                        System.IO.Path.Combine(configDestination, $"{Path.Name}-api-upstream.conf"),
                        Templates.TemplateType.Config,
                        ("NAME", $"{Path.Name}-api-upstream"),
                        ("LOCAL_IP", RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ProjectSettings.GetLocalIpAddress() : "host.docker.internal"),
                        ("UPSTREAM_PORT", api.Port.ToString()));
                    
                    break;
                case ProxyType.Client:
                    var client = context.ProjectSettings.Clients[Path.Name];
                    
                    if (!string.IsNullOrEmpty(client.Api))
                        break;
                    
                    Templates.Extract(
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

            return Task.FromResult(new BuildResult(true, ""));
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
        
        private enum ProxyType
        {
            Api,
            Client
        }
    }
}