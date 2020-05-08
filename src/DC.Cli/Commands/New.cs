using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class New
    {
        public static Task Execute(Options options)
        {
            var directory = Path.Combine(Environment.CurrentDirectory, options.Name);

            if (Directory.Exists(directory))
                throw new InvalidOperationException($"There is already a project called {options.Name}");

            Directory.CreateDirectory(directory);

            var initOptions = new Init.Options
            {
                Path = directory,
                NugetFeed = options.NugetFeed,
            };

            return Init.Execute(initOptions);
        }

        [Verb("new", HelpText = "Create a new project.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "The name of the project.")]
            public string Name { get; set; }

            [Option('f', "nuget-feed", Default = "", HelpText = "Nuget feed to publish packages to.")]
            public string NugetFeed { get; set; }
        }
    }
}