using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components.Cloudflare;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class CfWorker
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.GetWorkerPath(settings));

            await components.Initialize<CloudflareWorkerComponent, CloudflareWorkerComponentType.ComponentData>(
                new CloudflareWorkerComponentType.ComponentData(options.Name, options.Port, options.DestinationPort),
                settings);
        }
        
        [Verb("cf-worker")]
        public class Options
        {
            [Option('p', "path", Default = "[[PROJECT_ROOT]]/src", HelpText = "Where to put the worker.")]
            public string Path { private get; set; }

            [Option('n', "name", HelpText = "Name of worker.")]
            public string Name { get; set; }
            
            [Option('o', "port", HelpText = "Port to run the worker on.")]
            public int? Port { get; set; }
            
            [Option('d', "destination", Default = 4000, HelpText = "Destination port for the worker.")]
            public int DestinationPort { get; set; }

            public string GetWorkerPath(ProjectSettings settings)
            {
                return settings.GetRootedPath(System.IO.Path.Combine(Path, Name));
            }
        }
    }
}