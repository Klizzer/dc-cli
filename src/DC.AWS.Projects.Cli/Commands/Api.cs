using System.IO;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components.Aws.ApiGateway;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Api
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var apiPath = settings.GetRootedPath(Path.Combine(options.Path, options.Name));

            var components = await Components.Components.BuildTree(settings, apiPath);

            await components.Initialize<ApiGatewayComponent, ApiGatewayComponentType.ComponentData>(
                new ApiGatewayComponentType.ComponentData(
                    options.Name,
                    options.Port,
                    options.DefaultLanguage,
                    options.BaseUrl),
                settings);
        }
        
        [Verb("api", HelpText = "Create a api.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the api.")]
            public string Name { get; set; }

            [Option('b', "base-url", Default = "/", HelpText = "Base url for the api.")]
            public string BaseUrl { get; set; }
            
            [Option('p', "path", Default = "[[PROJECT_ROOT]]/src", HelpText = "Path where to put the api.")]
            public string Path { get; set; }

            [Option('l', "lang", HelpText = "Default language for api functions.")]
            public string DefaultLanguage { get; set; }

            [Option('o', "port", HelpText = "Port to run api on.")]
            public int? Port { get; set; }
        }
    }
}