using System;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components.Nginx;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class AddProxyPath
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            await LocalProxyComponentType.AddProxyPath(
                settings,
                settings.GetRootedPath(options.Path),
                options.BaseUrl,
                options.Port);
        }
        
        [Verb("add-proxy-path", HelpText = "Add a path to proxy.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to add a proxy to.")]
            public string Path { get; set; } = Environment.CurrentDirectory;
            
            [Option('o', "port", Required = true, HelpText = "Port to proxy to.")]
            public int Port { get; set; }

            [Option('u', "url", Default = "/", HelpText = "Url to proxy.")]
            public string BaseUrl { get; set; }
        }
    }
}