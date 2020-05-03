using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandLine;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class BuildFunction
    {
        private static readonly IImmutableDictionary<string, Action<Options, ProjectSettings, FunctionLanguage>>
            LanguageCompilers = new Dictionary<string, Action<Options, ProjectSettings, FunctionLanguage>>
                {
                    ["node"] = BuildNode,
                    ["go"] = BuildGo
                }
                .ToImmutableDictionary();
        
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();

            var functionRoot = settings.FindFunctionRoot(options.Path);
            
            var deserializer = new Deserializer();

            var infraData =
                deserializer.Deserialize<TemplateData>(File.ReadAllText(Path.Combine(functionRoot.path,
                    "function.infra.yml")));

            var function = infraData.Resources.Values.FirstOrDefault(x => x.Type == "AWS::Serverless::Function");

            if (function == null)
                return;

            var runtime = function.Properties["Runtime"].ToString();

            var language = FunctionLanguage.ParseFromRuntime(runtime);
            
            if (language == null || !LanguageCompilers.ContainsKey(language.Name))
                return;

            LanguageCompilers[language.Name](options, settings, language);
        }

        private static void BuildNode(Options options, ProjectSettings settings, FunctionLanguage language)
        {
            var functionRoot = settings.FindFunctionRoot(options.Path);

            if (!File.Exists(Path.Combine(functionRoot.path, "package.json")))
                return;
            
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "yarn",
                WorkingDirectory = functionRoot.path
            });

            process?.WaitForExit();
        }

        private static void BuildGo(Options options, ProjectSettings settings, FunctionLanguage language)
        {
            var functionRoot = settings.FindFunctionRoot(options.Path);
            
            var restoreProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "go",
                Arguments = "get -v -t -d ./...",
                WorkingDirectory = functionRoot.path
            });

            restoreProcess?.WaitForExit();

            var buildProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "go",
                Arguments = "build -o ./.out/main -v .",
                WorkingDirectory = functionRoot.path
            });

            buildProcess?.WaitForExit();
        }
        
        [Verb("build-func", HelpText = "Build a function.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to the function.")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
    }
}