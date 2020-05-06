using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public class ClientComponent : IComponent
    {
        private static readonly IImmutableDictionary<ClientType, Action<DirectoryInfo>> TypeHandlers =
            new Dictionary<ClientType, Action<DirectoryInfo>>
            {
                [ClientType.VueNuxt] = CreateVueNuxt
            }.ToImmutableDictionary();
        
        private ClientComponent(DirectoryInfo path)
        {
            Path = path;
        }

        public DirectoryInfo Path { get; }

        public static async Task<ClientComponent> InitAt(
            ProjectSettings settings,
            string path,
            string baseUrl,
            ClientType clientType,
            int? port)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (dir.Exists)
                throw new InvalidOperationException($"You can't create a client at \"{dir.FullName}\". It already exists.");
            
            dir.Create();

            settings.AddClient(
                dir.Name,
                baseUrl,
                settings.GetRelativePath(dir.FullName),
                clientType,
                port);

            await File.WriteAllTextAsync(System.IO.Path.Combine(dir.FullName, "client.infra.yml"), "");
            
            var clientServicePath = System.IO.Path.Combine(settings.ProjectRoot, $"services/{dir.Name}.client.make");

            if (!File.Exists(clientServicePath))
            {
                await Templates.Extract(
                    "client.make",
                    clientServicePath,
                    Templates.TemplateType.Services,
                    ("CLIENT_NAME", dir.Name),
                    ("PORT", settings.Clients[dir.Name].Port.ToString()),
                    ("CLIENT_PATH", settings.GetRelativePath(dir.FullName)));
            }
            
            TypeHandlers[clientType](dir);

            await settings.Save();
            
            return new ClientComponent(dir);
        }
        
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
        
        private static void CreateVueNuxt(DirectoryInfo dir)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "yarn",
                Arguments = $"create nuxt-app {dir.Name}",
                WorkingDirectory = dir.FullName
            });

            process?.WaitForExit();
        }
        
        private class PackageJsonData
        {
            public IImmutableDictionary<string, string> Scripts { get; set; }
        }
    }
}