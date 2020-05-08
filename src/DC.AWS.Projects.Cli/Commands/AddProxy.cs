using System;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components.Nginx;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class AddProxy
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();
            var components = await Components.Components.BuildTree(settings, options.Path);

            await components.Initialize<LocalProxyComponent, LocalProxyComponentType.ComponentData>(
                new LocalProxyComponentType.ComponentData(options.Name, options.Port), settings);
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