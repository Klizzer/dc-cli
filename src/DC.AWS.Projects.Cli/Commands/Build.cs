using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Build
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();
            
            var infrastructureDestination = Path.Combine(settings.ProjectRoot, "infrastructure/environment/.generated");
            var configDestination = Path.Combine(settings.ProjectRoot, "config/.generated");

            Directories.Recreate(infrastructureDestination);
            Directories.Recreate(configDestination);

            var components = Components.Components.FindComponents(settings, options.Path);

            var context = BuildContext.New(settings);

            var result = await components.Build(context);
            
            Console.Write(result.Output);

            if (!result.Success)
                throw new BuildFailedException(settings.GetRootedPath(options.Path));
            
            var initialTemplates = settings.GetRootedPath(options.Path) == settings.ProjectRoot
                ? new List<string>
                {
                    "project.yml"
                }
                : new List<string>();

            var templates = context.GetTemplates(initialTemplates.ToImmutableList());

            var serializer = new Serializer();
            
            foreach (var template in templates)
            {
                await File.WriteAllTextAsync(Path.Combine(infrastructureDestination, template.Key),
                    serializer.Serialize(template.Value));
            }
        }
        
        [Verb("build", HelpText = "Build the project.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to build")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class BuildFailedException : Exception
        {
            public BuildFailedException(string path) : base($"Build failed at: \"{path}\"")
            {
                
            }
        }
    }
}