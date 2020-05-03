using System.IO;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class ApiFunc
    {
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();

            var apiRoot = settings.FindApiPath(options.Api);
            
            Func.Execute(new Func.Options
            {
                Language = options.Language,
                Name = options.Name,
                Path = Path.Combine(apiRoot, options.Path ?? ""),
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
            
            [Option('l', "lang", HelpText = "Language to use for the function.")]
            public SupportedLanguage? Language { get; set; }
        }
    }
}