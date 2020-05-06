using System;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class AddProxy
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            await LocalProxyComponent.InitAt(
                settings,
                options.GetProxyPath(settings),
                options.Port);
        }
        
        [Verb("add-proxy")]
        public class Options
        {
            [Option('n', "name", HelpText = "Name of proxy.")]
            public string Name { get; set; }
            
            [Option('p', "path", HelpText = "Path to add a proxy to.")]
            public string Path { get; set; } = Environment.CurrentDirectory;

            [Option('o', "port", HelpText = "Port for proxy.")]
            public int? Port { get; set; }

            public string GetProxyPath(ProjectSettings settings)
            {
                return settings.GetRootedPath(System.IO.Path.Combine(Path, Name));
            }
        }
    }
}