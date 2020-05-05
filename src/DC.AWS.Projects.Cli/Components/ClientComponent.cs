using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public class ClientComponent : IComponent
    {
        private ClientComponent(DirectoryInfo path)
        {
            Path = path;
        }

        public DirectoryInfo Path { get; }
        
        public Task<BuildResult> Build(IBuildContext context)
        {
            return Task.FromResult(new BuildResult(true, ""));
        }

        public async Task<TestResult> Test()
        {
            if (!File.Exists(System.IO.Path.Combine(Path.FullName, "package.json")))
                return new TestResult(true, "");

            var packageData =
                Json.DeSerialize<PackageJsonData>(await File.ReadAllTextAsync(System.IO.Path.Combine(Path.FullName, "package.json")));

            if (!(packageData.Scripts ?? new Dictionary<string, string>().ToImmutableDictionary())
                .ContainsKey("test"))
            {
                return new TestResult(true, "");
            }

            var executionResult = await ProcessExecutor
                .ExecuteBackground("yarn", "run test", Path.FullName);
                
            return new TestResult(executionResult.ExitCode == 0, executionResult.Output);
        }
        
        public static IEnumerable<ClientComponent> FindAtPath(DirectoryInfo path)
        {
            if (File.Exists(System.IO.Path.Combine(path.FullName, "client.infra.yml")))
                yield return new ClientComponent(path);
        }
        
        private class PackageJsonData
        {
            public IImmutableDictionary<string, string> Scripts { get; set; }
        }
    }
}