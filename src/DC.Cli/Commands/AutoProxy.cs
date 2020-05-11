using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using DC.Cli.Components;
using DC.Cli.Components.Nginx;

namespace DC.Cli.Commands
{
    public static class AutoProxy
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, settings.GetRootedPath(options.Path));

            var rootHttpEndpoints = components.FindAllFirstLevel<IHaveHttpEndpoint>();

            foreach (var endpoint in rootHttpEndpoints)
            {
                var clients = endpoint.tree.FindAll<IHaveHttpEndpoint>(Components.Components.Direction.In);
                
                if (clients.All(x => x.component == endpoint.component))
                    continue;
                
                var proxyPath = endpoint.tree.Path.FullName;

                if (!LocalProxyComponentType.HasProxyAt(proxyPath))
                {
                    int? port = null;

                    if (!options.AssignPorts)
                    {
                        Console.WriteLine($"Adding proxy for component: {endpoint.component.Name}. Please enter a port to use:");
                        port = int.Parse(Console.ReadLine() ?? "");
                    }

                    await endpoint.tree.Initialize<LocalProxyComponent, LocalProxyComponentType.ComponentData>(
                        new LocalProxyComponentType.ComponentData(endpoint.component.Name, port),
                        settings);
                }
                
                foreach (var client in clients)
                {
                    if (LocalProxyComponentType.HasProxyPathFor(proxyPath, client.component.Port))
                        continue;

                    await LocalProxyComponentType.AddProxyPath(
                        settings, 
                        proxyPath,
                        client.component.BaseUrl,
                        client.component.Port);
                }
            }
        }
        
        [Verb("auto-proxy", HelpText = "Creates local proxies for available resources.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to search.")]
            public string Path { get; set; } = Environment.CurrentDirectory;

            [Option('a', "assign-ports", HelpText = "Assign ports randomly.")]
            public bool AssignPorts { get; set; }
        }
    }
}