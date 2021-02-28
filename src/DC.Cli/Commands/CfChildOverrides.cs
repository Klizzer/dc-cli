using System;
using System.Threading.Tasks;
using CommandLine;
using DC.Cli.Components.Aws.ChildConfig;

namespace DC.Cli.Commands
{
    public static class CfChildOverrides
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.Path);

            await components.Initialize<ChildConfigComponent, ChildConfigComponentType.ComponentData>(
                new ChildConfigComponentType.ComponentData(options.Name), settings);
        }

        [Verb("cf-config-overrides", HelpText = "Add cloudformation config overrides")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path where to put the overrides.")]
            public string Path { get; set; } = Environment.CurrentDirectory;
            
            [Option('n', "name", Required = true, HelpText = "Name of the overrides.")]
            public string Name { get; set; }
        }
    }
}