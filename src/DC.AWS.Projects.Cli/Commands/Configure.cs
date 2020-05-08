using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Configure
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = Components.Components.BuildTree(settings, settings.GetRootedPath(""));

            var requiredConfigurations = components
                .FindAll<INeedConfiguration>(Components.Components.Direction.In)
                .SelectMany(x => x.component.GetRequiredConfigurations())
                .ToImmutableList();
            
            var newConfigurations = new Dictionary<string, (string value, INeedConfiguration.ConfigurationType configurationType)>();

            foreach (var requiredConfiguration in requiredConfigurations)
            {
                if (newConfigurations.ContainsKey(requiredConfiguration.key))
                {
                    if (requiredConfiguration.configurationType <
                        newConfigurations[requiredConfiguration.key].configurationType)
                    {
                        newConfigurations[requiredConfiguration.key] = (
                            newConfigurations[requiredConfiguration.key].value,
                            requiredConfiguration.configurationType);
                    }
                    
                    continue;
                }
                
                if (settings.HasConfiguration(requiredConfiguration.key) && !options.Force)
                    continue;

                var value = ConsoleInput.Ask(requiredConfiguration.question);

                newConfigurations[requiredConfiguration.key] = (value, requiredConfiguration.configurationType);
            }

            foreach (var newConfiguration in newConfigurations)
            {
                settings.SetConfiguration(newConfiguration.Key, newConfiguration.Value.value,
                    newConfiguration.Value.configurationType);
            }

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