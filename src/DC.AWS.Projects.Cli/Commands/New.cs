using System;
using System.IO;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class New
    {
        public static void Execute(Options options)
        {
            var directory = Path.Combine(Environment.CurrentDirectory, options.Name);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            var initOptions = new Init.Options
            {
                Path = directory,
                Language = options.Language,
                NugetFeed = options.NugetFeed,
                AwsRegion = options.AwsRegion
            };
            
            Init.Execute(initOptions);
        }
        
        [Verb("new", HelpText = "Create a new project.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "The name of the project.")]
            public string Name { get; set; }
            
            [Option('l', "lang", Default = FunctionLanguage.DefaultLanguage, HelpText = "Default language to use for functions.")]
            public string Language { get; set; }
            
            [Option('f', "nuget-feed", Default = "", HelpText = "Nuget feed to publish packages to.")]
            public string NugetFeed { get; set; }
            
            [Option('r', "aws-region", Default = "eu-north-1", HelpText = "Aws region.")]
            public string AwsRegion { get; set; }
        }
    }
}