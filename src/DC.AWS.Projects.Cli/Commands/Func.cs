using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Func
    {
        private static readonly IImmutableDictionary<FunctionTrigger, Action<Options, ProjectSettings>> TriggerHandlers =
            new Dictionary<FunctionTrigger, Action<Options, ProjectSettings>>
            {
                [FunctionTrigger.Api] = SetupApiTrigger
            }.ToImmutableDictionary();
        
        public static void Execute(Options options)
        {
            var projectSettings = ProjectSettings.Read();
            
            if (Directory.Exists(options.GetRootedFunctionPath(projectSettings)))
                throw new InvalidOperationException($"You can't add a new function at: \"{options.GetRootedFunctionPath(projectSettings)}\". It already exists.");
                
            Directory.CreateDirectory(options.GetRootedFunctionPath(projectSettings));

            var language = options.GetLanguage(projectSettings);
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            Directories.Copy(Path.Combine(executingAssembly.GetPath(), $"Templates/Functions/{language}"), options.GetRootedFunctionPath(projectSettings));

            TriggerHandlers[options.Trigger ?? FunctionTrigger.Api](options, projectSettings);
        }

        private static void SetupApiTrigger(Options options, ProjectSettings settings)
        {
            var apiRoot = settings.FindApiRoot(options.GetRootedFunctionPath(settings));
            
            var languageRuntimes = new Dictionary<SupportedLanguage, string>
            {
                [SupportedLanguage.Node] = "nodejs12.x"
            };
            
            Console.WriteLine("Enter url:");
            var url = settings.GetApiFunctionUrl(apiRoot.name, Console.ReadLine());

            Console.WriteLine("Enter method:");
            var method = Console.ReadLine();
            
            Templates.Extract(
                "api-function.infra.yml",
                Path.Combine(options.GetRootedFunctionPath(settings), "function.infra.yml"),
                Templates.TemplateType.Infrastructure,
                ("FUNCTION_NAME", options.Name),
                ("FUNCTION_RUNTIME", languageRuntimes[options.GetLanguage(settings)]),
                ("FUNCTION_METHOD", method),
                ("FUNCTION_PATH", options.GetRelativeFunctionPath(settings, apiRoot.name)),
                ("API_NAME", apiRoot.name),
                ("URL", url));
        }
        
        [Verb("func", HelpText = "Create a function.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the function.")]
            public string Name { get; set; }
            
            [Option('l', "lang", HelpText = "Language to use for the function.")]
            public SupportedLanguage? Language { private get; set; }

            [Option('t', "trigger", HelpText = "Trigger type for the function.")]
            public FunctionTrigger? Trigger { get; set; }
            
            [Option('p', "path", Default = "[[PROJECT_ROOT]]/src", HelpText = "Path where to put the function.")]
            public string Path { private get; set; }

            public string GetRootedFunctionPath(ProjectSettings settings)
            {
                return System.IO.Path.Combine(settings.GetRootedPath(Path), Name);
            }
            
            public string GetRelativeFunctionPath(ProjectSettings settings, string api)
            {
                var apiPath = settings.GetRootedPath(settings.Apis[api].RelativePath);
                
                var dir = new DirectoryInfo(GetRootedFunctionPath(settings).Substring(apiPath.Length));

                return dir.FullName.Substring(1);
            }

            public SupportedLanguage GetLanguage(ProjectSettings settings, string api = null)
            {
                return Language ?? settings.GetLanguage(api);
            }
        }
    }
}