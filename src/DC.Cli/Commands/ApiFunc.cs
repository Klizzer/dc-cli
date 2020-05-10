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
                settings.GetRootedPath(options.Path));

            var path = options.Path;

            if (!string.IsNullOrEmpty(options.Api))
            {
                var apiComponent = components.FindFirst<ApiGatewayComponent>(
                    Components.Components.Direction.Out,
                    options.Api);

                if (apiComponent == null)
                {
                    apiComponent = components.FindFirst<ApiGatewayComponent>(
                        Components.Components.Direction.In,
                        options.Api);
                    
                    if (apiComponent == null)
                        throw new InvalidOperationException($"Can't find a api named {options.Api}");

                    path = Path.IsPathRooted(path) 
                        ? apiComponent.Path.FullName 
                        : Path.Combine(apiComponent.Path.FullName, path ?? "");
                }
            }

            await Func.Execute(new Func.Options
            {
                Language = options.Language,
                Name = options.Name,
                Path = path,
                Trigger = FunctionTrigger.Api
            });
        }
        
        [Verb("api-func", HelpText = "Create a api-function.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the function.")]
            public string Name { get; set; }
            
            [Option('a', "api", HelpText = "Api to add function to.")]
            public string Api { get; set; }

            [Option('p', "path", HelpText = "Path to put function.")]
            public string Path { get; set; } = Environment.CurrentDirectory;
            
            [Option('l', "lang", HelpText = "Language to use for the function.")]
            public string Language { get; set; }
        }
    }
}