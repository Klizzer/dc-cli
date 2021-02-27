using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli
{
    public class PythonLanguage : ILanguage
    {
        private const string LanguageName = "python";

        public string Name { get; } = LanguageName;
        
        public static ILanguage Instance { get; } = new PythonLanguage();

        public static ILanguageVersion Python3_8 { get; } = new PythonVersion("3.8", "python3.8", "3.8-buster");
        
        public IEnumerable<ILanguageVersion> GetVersions()
        {
            yield return Python3_8;
        }

        public ILanguageVersion GetDefaultVersion()
        {
            return Python3_8;
        }
        
        private class PythonVersion : ILanguageVersion
        {
            private readonly string _runtimeName;
            private readonly Docker.Container _dockerContainer;
            
            public PythonVersion(string version, string runtimeName, string dockerImageTag)
            {
                Version = version;
                _runtimeName = runtimeName;
                
                _dockerContainer = Docker
                    .TemporaryContainerFromImage($"python:{dockerImageTag}")
                    .EntryPoint("python");
            }
            
            public string Language { get; } = LanguageName;
            public string Version { get; }
            
            public async Task<bool> Restore(string path)
            {
                var requirementsFile = Path.Combine(path, "requirements.txt");

                if (!File.Exists(requirementsFile))
                    return true;
                
                return await _dockerContainer
                    .WithVolume(path, "/usr/local/src", true)
                    .EntryPoint("pip")
                    .Run("install -r requirements.txt");
            }

            public Task<bool> Build(string path)
            {
                return Task.FromResult(true);
            }

            public Task<bool> Test(string path)
            {
                return _dockerContainer
                    .WithVolume(path, "/usr/local/src", true)
                    .Run("-m test");
            }

            public Task<bool> StartWatch(string path)
            {
                return Task.FromResult(true);
            }

            public Task<bool> StopWatch(string path)
            {
                return Task.FromResult(true);
            }

            public string GetHandlerName()
            {
                return "handler.handler";
            }

            public string GetFunctionOutputPath(string functionPath)
            {
                return functionPath;
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