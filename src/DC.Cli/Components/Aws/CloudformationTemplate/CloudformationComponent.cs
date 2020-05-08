using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components.Aws.CloudformationTemplate
{
    public class CloudformationComponent : ICloudformationComponent
    {
        private readonly DirectoryInfo _path;
        private readonly string _fileName;

        public CloudformationComponent(DirectoryInfo path, string fileName)
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
    }
}