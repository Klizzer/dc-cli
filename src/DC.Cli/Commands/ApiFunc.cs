using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using DC.Cli.Components.Aws.ApiGateway;

namespace DC.Cli.Commands
{
    public static class ApiFunc
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(
                settings,
                settings.GetRootedPath(options.Root));

            var apiComponent = components.FindFirst<ApiGatewayComponent>(
                Components.Components.Direction.In,
                options.Api);

            if (apiComponent == null)
            {
                throw new InvalidOperationException(
                    $"Can't find a api named {options.Api} at path {settings.GetRootedPath(options.Root)}");
            }

            await Func.Execute(new Func.Options
            {
                Language = options.Language,
                Name = options.Name,
                Path = Path.Combine(apiComponent.Path.FullName, options.Path ?? ""),
                Trigger = FunctionTrigger.Api
            });
        }
        
        [Verb("api-func", HelpText = "Create a api-function.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the function.")]
            public string Name { get; set; }
            
            [Option('a', "api", Required = true, HelpText = "Api to add function to.")]
            public string Api { get; set; }

            [Option('p', "path", HelpText = "Relative path from api path.")]
            public string Path { get; set; }

            [Option('r', "root", HelpText = "Root path to look for api.")]
            public string Root { get; set; } = Environment.CurrentDirectory;
            
            [Option('l', "lang", HelpText = "Language to use for the function.")]
            public string Language { get; set; }
        }
    }
}