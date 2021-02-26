using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.Cli
{
    public class NodeLanguage : ILanguage
    {
        public const string LanguageName = "node";

        private NodeLanguage()
        {
            
        }

        public static ILanguage Instance { get; } = new NodeLanguage();
        private static ILanguageVersion Node10 { get; } = new NodeVersion("10", "nodejs10.x", "10");
        private static ILanguageVersion Node12 { get; } = new NodeVersion("12", "nodejs12.x", "12");

        public string Name { get; } = LanguageName;

        public IEnumerable<ILanguageVersion> GetVersions()
        {
            yield return Node10;
            yield return Node12;
        }

        public ILanguageVersion GetDefaultVersion()
        {
            return Node12;
        }
        
        private class NodeVersion : ILanguageVersion
        {
            private readonly string _runtimeName;
            private readonly Docker.Container _dockerContainer;
            
            public NodeVersion(string version, string runtimeName, string dockerImageTag)
            {
                Version = version;
                _runtimeName = runtimeName;

                _dockerContainer = Docker
                    .TemporaryContainerFromImage($"node:{dockerImageTag}")
                    .EntryPoint("yarn");
            }

            public string Language { get; } = LanguageName;
            public string Version { get; }

            public async Task<bool> Restore(string path)
            {
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return true;
                
                return await _dockerContainer
                    .WithVolume(path, "/usr/src/app", true)
                    .Run("");
            }

            public async Task<bool> Build(string path)
            {
                var restoreResult = await Restore(path);

                if (!restoreResult)
                    return false;
                
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return true;

                var packageData =
                    Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(path, "package.json")));

                if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                    .ContainsKey("build"))
                {
                    return true;
                }

                await Restore(path);
                
                return await _dockerContainer
                    .WithVolume(path, "/usr/src/app", true)
                    .Run("run build");
            }

            public async Task<bool> Test(string path)
            {
                var restoreResult = await Restore(path);

                if (!restoreResult)
                    return false;
                
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return true;

                var packageData =
                    Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(path, "package.json")));

                if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                    .ContainsKey("test"))
                {
                    return true;
                }

                await Restore(path);
                
                return await _dockerContainer
                    .WithVolume(path, "/usr/src/app", true)
                    .Run("run test");
            }

            public async Task<bool> StartWatch(string path)
            {
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return true;

                var packageData =
                    Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(Path.Combine(path, "package.json")));

                if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                    .ContainsKey("watch"))
                {
                    return true;
                }
                
                return await _dockerContainer
                    .WithName(GetDockerName(path))
                    .WithVolume(path, "/usr/src/app", true)
                    .Detached()
                    .Run("run watch");
            }

            public Task<bool> StopWatch(string path)
            {
                Docker.Remove(GetDockerName(path));

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

            private static string GetDockerName(string path)
            {
                return $"nodewatch-{path.GetStableHashCode():X}";
            }
            
            private class PackageJsonData
            {
                public IImmutableDictionary<string, string> Scripts { get; set; }
            }
        }
    }
}