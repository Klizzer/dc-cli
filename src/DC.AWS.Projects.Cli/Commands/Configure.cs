using System;
using System.IO;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Configure
    {
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();

            if (File.Exists(Path.Combine(settings.ProjectRoot, ".settings.json")))
            {
                if (options.SkipExisting)
                    return;

                if (!options.Force)
                {
                    Console.WriteLine(
                        "This project is already configured. Do you want to overwrite the current configuration? (yes, no)");
                    var overwrite = Console.ReadLine() == "yes";

                    if (!overwrite)
                        return;
                }
            }

            var localstackApiKey = options.LocalstackApiKey;

            if (string.IsNullOrEmpty(localstackApiKey))
            {
                Console.WriteLine("Enter your localstack api key if you have any:");

                localstackApiKey = Console.ReadLine();
            }

            var config = new ProjectConfiguration
            {
                LocalstackApiKey = localstackApiKey
            };
            
            File.WriteAllText(Path.Combine(settings.ProjectRoot, ".settings.json"), Json.Serialize(config));
        }
        
        [Verb("configure", HelpText = "Configure your environment.")]
        public class Options
        {
            [Option('k', "localstack-key", Default = "", HelpText = "Localstack api key.")]
            public string LocalstackApiKey { get; set; }

            [Option('f', "force", Default = false, HelpText = "Overwrite existing configuration without asking.")]
            public bool Force { get; set; }

            [Option('s', "skip-existing", Default = false, HelpText = "Skip if already configured.")]
            public bool SkipExisting { get; set; }
        }
    }
}