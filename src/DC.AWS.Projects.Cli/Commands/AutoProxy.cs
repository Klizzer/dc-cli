using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class AutoProxy
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = Components.Components.BuildTree(settings, settings.GetRootedPath(options.Path));

            var rootHttpEndpoints = components.FindAllFirstLevel<IHaveHttpEndpoint>();

            foreach (var endpoint in rootHttpEndpoints)
            {
                var proxyPath = settings.GetRootedPath(Path.Combine($"proxy/{endpoint.component.Name}"));

                if (!LocalProxyComponent.HasProxyAt(proxyPath))
                {
                    int? port = null;

                    if (!options.AssignPorts)
                    {
                        Console.WriteLine($"Adding proxy for component: {endpoint.component.Name}. Please enter a port to use:");
                        port = int.Parse(Console.ReadLine() ?? "");
                    }

                    await LocalProxyComponent.InitAt(settings, proxyPath, port);
                }

                if (!LocalProxyComponent.HasProxyPathFor(proxyPath, endpoint.component.Port))
                {
                    await LocalProxyComponent.AddProxyPath(
                        settings,
                        proxyPath,
                        endpoint.component.BaseUrl,
                        endpoint.component.Port);
                }

                var clients = endpoint.tree.FindAll<IHaveHttpEndpoint>(Components.Components.Direction.In);

                foreach (var client in clients)
                {
                    if (LocalProxyComponent.HasProxyPathFor(proxyPath, client.component.Port))
                        continue;

                    await LocalProxyComponent.AddProxyPath(
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