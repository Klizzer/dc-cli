using System.Threading.Tasks;
using CommandLine;

namespace DC.Cli.Commands
{
    public static class Configure
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, settings.GetRootedPath(""));

            await components.Configure(settings, true, options.Force);

            await settings.Save();
        }
        
        [Verb("configure", HelpText = "Configure your environment.")]
        public class Options
        {
            [Option('f', "force", Default = false, HelpText = "Reconfigure project.")]
            public bool Force { get; set; }
        }
    }
}