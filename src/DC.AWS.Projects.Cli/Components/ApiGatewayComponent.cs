using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class ApiGatewayComponent : IComponent
    {
        private ApiGatewayComponent(DirectoryInfo path)
        {
            Path = path;
        }

        public DirectoryInfo Path { get; }

        public static async Task<ApiGatewayComponent> InitAt(
            ProjectSettings settings,
            string path,
            string baseUrl,
            string language,
            int externalPort)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (settings.Apis.ContainsKey(dir.Name))
                throw new InvalidOperationException($"There is already a api named: \"{dir.Name}\"");
            
            if (Directory.Exists(settings.GetRootedPath(dir.FullName)))
                throw new InvalidOperationException($"You can't add a new api at: \"{dir.FullName}\". It already exists.");

            dir.Create();

            var runtime = FunctionLanguage.Parse(language);
            
            settings.AddApi(
                dir.Name,
                baseUrl,
                settings.GetRelativePath(dir.FullName),
                runtime, 
                externalPort);
            
            await Templates.Extract(
                "api.infra.yml",
                System.IO.Path.Combine(dir.FullName, "api.infra.yml"),
                Templates.TemplateType.Infrastructure,
                ("API_NAME", dir.Name));
            
            var apiServicePath = System.IO.Path.Combine(settings.ProjectRoot, $"services/{dir.Name}.api.make");

            if (!File.Exists(apiServicePath))
            {
                await Templates.Extract(
                    "api.make",
                    apiServicePath,
                    Templates.TemplateType.Services,
                    ("API_NAME", dir.Name));   
            }
            
            await settings.Save();
            
            return new ApiGatewayComponent(dir);
        }
        
        public async Task<BuildResult> Build(IBuildContext context)
        {
            var deserializer = new Deserializer();

            var infraData =
                deserializer.Deserialize<TemplateData>(await File.ReadAllTextAsync(System.IO.Path.Combine(Path.FullName, "api.infra.yml")));
            
            context.AddTemplate($"{Path.Name}.api.yml");
            context.ExtendTemplate(infraData);
 
            return new BuildResult(true, "");
        }

        public Task<TestResult> Test()
        {
            return Task.FromResult(new TestResult(true, ""));
        }

        public static IEnumerable<ApiGatewayComponent> FindAtPath(DirectoryInfo path)
        {
            if (File.Exists(System.IO.Path.Combine(path.FullName, "api.infra.yml")))
                yield return new ApiGatewayComponent(path);
        }
    }
}