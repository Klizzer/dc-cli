using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli
{
    public class NodeLanguage : ILanguage
    {
        public const string LanguageName = "node";

        private NodeLanguage()
        {
            
        }

        public static ILanguage Instance { get; } = new NodeLanguage();
        private static ILanguageRuntime Node10 { get; } = new NodeRuntime("nodejs10.x");
        private static ILanguageRuntime Node12 { get; } = new NodeRuntime("nodejs12.x");

        public string Name { get; } = LanguageName;

        public IEnumerable<ILanguageRuntime> GetRuntimes()
        {
            yield return Node10;
            yield return Node12;
        }

        public ILanguageRuntime GetDefaultRuntime()
        {
            return Node12;
        }
        
        private class NodeRuntime : ILanguageRuntime
        {
            public NodeRuntime(string runtime)
            {
                Name = runtime;
            }

            public string Language { get; } = LanguageName;
            public string Name { get; }
            
            public async Task<BuildResult> Build(string path)
            {
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return new BuildResult(true, "");

                var executionResult = await ProcessExecutor
                    .ExecuteBackground("yarn", "", path);

                return new BuildResult(executionResult.ExitCode == 0, executionResult.Output);
            }

            public async Task<TestResult> Test(string path)
            {
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return new TestResult(true, "");

                var packageData =
                    Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(path, "package.json")));

                if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                    .ContainsKey("test"))
                {
                    return new TestResult(true, "");
                }

                var executionResult = await ProcessExecutor
                    .ExecuteBackground("yarn", "run test", path);
                
                return new TestResult(executionResult.ExitCode == 0, executionResult.Output);
            }

            public string GetHandlerName()
            {
                return "handler.handler";
            }

            public string GetFunctionOutputPath(string functionPath)
            {
                return functionPath;
            }

            public override string ToString()
            {
                return $"{Language}:{Name}";
            }
            
            private class PackageJsonData
            {
                public IImmutableDictionary<string, string> Scripts { get; set; }
            }
        }
    }
}