using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli
{
    public class GoLanguage : ILanguage
    {
        private const string LanguageName = "go";

        public string Name { get; } = LanguageName;
        
        public static ILanguage Instance { get; } = new GoLanguage();
        
        private static ILanguageVersion Go1 { get; } = new GoVersion("1", "go1.x", "1.14");
        
        public IEnumerable<ILanguageVersion> GetVersions()
        {
            yield return Go1;
        }

        public ILanguageVersion GetDefaultVersion()
        {
            return Go1;
        }
        
        private class GoVersion : ILanguageVersion
        {
            private readonly string _runtimeName;
            private readonly Docker.Container _dockerContainer;
            
            public GoVersion(string version, string runtimeName, string dockerImageTag)
            {
                Version = version;
                _runtimeName = runtimeName;

                _dockerContainer = Docker
                    .CreateContainer($"golang:{dockerImageTag}")
                    .EntryPoint("go");
            }

            public string Language { get; } = LanguageName;
            public string Version { get; }
            
            public async Task<BuildResult> Build(string path)
            {
                await Restore(path);

                var result = await _dockerContainer
                    .WithVolume(path, "/usr/local/src", true)
                    .Run("build -o ./.out/main -v .");
                
                return new BuildResult(result.exitCode == 0, result.output);
            }

            public async Task<TestResult> Test(string path)
            {
                await Restore(path);
                
                var result = await _dockerContainer
                    .WithVolume(path, "/usr/local/src", true)
                    .Run("test -run ''");

                return new TestResult(result.exitCode == 0, result.output);
            }

            private Task Restore(string path)
            {
                return _dockerContainer
                    .WithVolume(path, "/usr/local/src", true)
                    .Run("get -v -t -d ./...");
            }

            public string GetHandlerName()
            {
                return "main";
            }

            public string GetFunctionOutputPath(string functionPath)
            {
                return Path.Combine(functionPath, ".out");
            }

            public string GetRuntimeName()
            {
                return _runtimeName;
            }

            public override string ToString()
            {
                return $"{Language}:{Version}";
            }
        }
    }
}