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

        public static ILanguageVersion Python2_7 { get; } = new PythonVersion("2.7", "python2.7", "2.7-buster");
        public static ILanguageVersion Python3_6 { get; } = new PythonVersion("3.6", "python3.6", "3.6-buster");
        public static ILanguageVersion Python3_7 { get; } = new PythonVersion("3.7", "python3.7", "3.7-buster");
        public static ILanguageVersion Python3_8 { get; } = new PythonVersion("3.8", "python3.8", "3.8-buster");
        
        public IEnumerable<ILanguageVersion> GetVersions()
        {
            yield return Python2_7;
            yield return Python3_6;
            yield return Python3_7;
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
                    .Run("install -r requirements.txt --target ./.out --upgrade");
            }

            public async Task<bool> Build(string path)
            {
                var restoreSuccess = await Restore(path);

                if (!restoreSuccess)
                    return false;
                
                return await _dockerContainer
                    .WithVolume(path, "/usr/local/src", true)
                    .EntryPoint("/bin/bash")
                    .Run("-c 'for file in *.py; do cp \"$file\" \".out/${file}\";done'");
            }

            public async Task<bool> Test(string path)
            {
                var build = await Build(path);

                if (!build)
                    return false;
                
                return await _dockerContainer
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