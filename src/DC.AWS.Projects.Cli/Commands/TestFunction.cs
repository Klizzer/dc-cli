using System;
using System.IO;
using System.Linq;
using CommandLine;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class TestFunction
    {
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

            var languageRuntime = FunctionLanguage.ParseFromRuntime(runtime);

            var success = languageRuntime?.Test(functionRoot.path) ?? true;

            if (!success)
                throw new TestsFailedException(options.Path);
        }
        
        [Verb("test-func", HelpText = "Test a function.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to the function.")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
    }
}