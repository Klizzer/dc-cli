using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class CloudformationComponent : ICloudformationComponent
    {
        private readonly DirectoryInfo _path;
        private readonly string _fileName;

        private CloudformationComponent(DirectoryInfo path, string fileName)
        {
            _path = path;
            _fileName = fileName;
        }

        public string Name => _path.Name;

        public async Task<TemplateData> GetCloudformationData()
        {
            var deserializer = new Deserializer();

            return deserializer.Deserialize<TemplateData>(
                await File.ReadAllTextAsync(Path.Combine(_path.FullName, _fileName)));
        }

        public static IEnumerable<CloudformationComponent> FindAtPath(DirectoryInfo path)
        {
            return from file in path.EnumerateFiles() 
                where file.Name.EndsWith(".cf.yml")
                select new CloudformationComponent(path, file.Name);
        }
    }
}