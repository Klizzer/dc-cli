using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components.Aws.CloudformationStack;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class CloudformationStack
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.Path);

            await components.Initialize<CloudformationStackComponent, CloudformationStackComponentType.ComponentData>(
                new CloudformationStackComponentType.ComponentData(
                    options.Name,
                    options.GetServices(),
                    options.MainPort,
                    options.ServicesPort,
                    options.AwsRegion),
                settings);
        }
        
        [Verb("cf-stack", HelpText = "Setup a cloudformation stack environment.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path where to put localstack environment.")]
            public string Path { get; set; } = Environment.CurrentDirectory;

            [Option('n', "name", Required = true, HelpText = "Name of localstack environment.")]
            public string Name { get; set; }

            [Option('o', "port", Default = 8055, HelpText = "Port for admin UI.")]
            public int MainPort { get; set; }

            [Option("services-port", Default = 4566, HelpText = "Port to run services on.")]
            public int ServicesPort { get; set; }

            [Option('s', "services", Required = true, Default = "edge,serverless", HelpText = "Services to start.")]
            public string Services { private get; set; }

            [Option('r', "aws-region", Required = true, Default = "eu-north-1", HelpText = "Aws region for the stack")]
            public string AwsRegion { get; set; }

            public IImmutableList<string> GetServices()
            {
                return Services.Split(',').ToImmutableList();
            }
        }
    }
}