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