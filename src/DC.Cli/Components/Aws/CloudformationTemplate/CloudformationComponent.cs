using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.CloudformationTemplate
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
        
        public async Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations()
        {
            var template = await GetCloudformationData();

            return await template.GetRequiredConfigurations();
        }

        public async Task<TemplateData> GetCloudformationData()
        {
            var deserializer = new Deserializer();

            return deserializer.Deserialize<TemplateData>(
                await File.ReadAllTextAsync(Path.Combine(_path.FullName, _fileName)));
        }
    }
}