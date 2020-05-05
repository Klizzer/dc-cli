using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class CloudformationComponent : IComponent
    {
        private readonly string _fileName;

        private CloudformationComponent(DirectoryInfo path, string fileName)
        {
            Path = path;
            _fileName = fileName;
        }

        public DirectoryInfo Path { get; }
        
        public async Task<BuildResult> Build(IBuildContext context)
        {
            var deserializer = new Deserializer();

            var infraData =
                deserializer.Deserialize<TemplateData>(await File.ReadAllTextAsync(System.IO.Path.Combine(Path.FullName, _fileName)));
            
            context.ExtendTemplate(infraData);

            return new BuildResult(true, "");
        }

        public Task<TestResult> Test()
        {
            return Task.FromResult(new TestResult(true, ""));
        }

        public static IEnumerable<CloudformationComponent> FindAtPath(DirectoryInfo path)
        {
            return from file in path.EnumerateFiles() 
                where file.Name.EndsWith(".cf.yml")
                select new CloudformationComponent(path, file.Name);
        }
    }
}