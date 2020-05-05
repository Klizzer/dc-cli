using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli
{
    public class GoLanguage : ILanguage
    {
        public const string LanguageName = "go";

        public string Name { get; } = LanguageName;
        
        public static ILanguage Instance { get; } = new GoLanguage();
        
        private static ILanguageRuntime Go1 { get; } = new GoRuntime("go1.x");
        
        public IEnumerable<ILanguageRuntime> GetRuntimes()
        {
            yield return Go1;
        }

        public ILanguageRuntime GetDefaultRuntime()
        {
            return Go1;
        }
        
        private class GoRuntime : ILanguageRuntime
        {
            public GoRuntime(string name)
            {
                Name = name;
            }

            public string Language { get; } = LanguageName;
            public string Name { get; }
            
            public async Task<BuildResult> Build(string path)
            {
                await Restore(path);

                var result = await ProcessExecutor.ExecuteBackground("go", "build -o ./.out/main -v .", path);

                return new BuildResult(result.ExitCode == 0, result.Output);
            }

            public async Task<TestResult> Test(string path)
            {
                await Restore(path);

                var result = await ProcessExecutor.ExecuteBackground("go", "test -run ''", path);
                
                return new TestResult(result.ExitCode == 0, result.Output);
            }

            private static Task Restore(string path)
            {
                return ProcessExecutor.ExecuteBackground("go", "get -v -t -d ./...", path);
            }

            public string GetHandlerName()
            {
                return "main";
            }

            public string GetFunctionOutputPath(string functionPath)
            {
                return Path.Combine(functionPath, ".out");
            }
            
            public override string ToString()
            {
                return $"{Language}:{Name}";
            }
        }
    }
}