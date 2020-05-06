using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Func
    {
        public static async Task Execute(Options options)
        {
            var projectSettings = await ProjectSettings.Read();

            var components = Components.Components.BuildTree(
                projectSettings,
                projectSettings.GetRootedPath(options.Path));

            await LambdaFunctionComponent.InitAt(
                projectSettings, 
                options.Trigger ?? FunctionTrigger.Api,
                options.Language,
                projectSettings.GetRootedPath(options.Path),
                components);
        }
        
        [Verb("func", HelpText = "Create a function.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the function.")]
            public string Name { get; set; }
            
            [Option('l', "lang", HelpText = "Language to use for the function.")]
            public string Language { get; set; }

            [Option('t', "trigger", HelpText = "Trigger type for the function.")]
            public FunctionTrigger? Trigger { get; set; }
            
            [Option('p', "path", Default = "[[PROJECT_ROOT]]/src", HelpText = "Path where to put the function.")]
            public string Path { get; set; }
        }
    }
}