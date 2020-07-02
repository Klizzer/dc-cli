using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.LambdaLayer
{
    public class LambdaLayerComponent : ICloudformationComponent,
        IRestorableComponent,
        IBuildableComponent,
        ITestableComponent
    {
        public const string ConfigFileName = "lambda-func.config.yml";
        
        private readonly DirectoryInfo _path;
        private readonly LambdaLayerConfiguration _configuration;
        
        private LambdaLayerComponent(DirectoryInfo path, LambdaLayerConfiguration configuration)
        {
            _path = path;
            _configuration = configuration;
        }
        
        public string Name => _configuration.Name;
        
        public Task<bool> Test()
        {
            return _configuration.GetLanguage().Test(_path.FullName);
        }

        public Task<bool> Build()
        {
            return _configuration.GetLanguage().Build(_path.FullName);
        }

        public Task<bool> Restore()
        {
            return _configuration.GetLanguage().Restore(_path.FullName);
        }

        public Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations()
        {
            return _configuration.Settings.Template.GetRequiredConfigurations();
        }

        public Task<TemplateData> GetCloudformationData()
        {
            return Task.FromResult(_configuration.Settings.Template);
        }
        
        public static async Task<LambdaLayerComponent> Init(DirectoryInfo path)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                return null;
            
            var deserializer = new Deserializer();
            return new LambdaLayerComponent(
                path,
                deserializer.Deserialize<LambdaLayerConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName))));
        }
        
        private class LambdaLayerConfiguration
        {
            public string Name { get; set; }
            public LambdaLayerSettings Settings { get; set; }

            public ILanguageVersion GetLanguage()
            {
                return FunctionLanguage.Parse(Settings.Language);
            }
            
            public class LambdaLayerSettings
            {
                public string Type { get; set; }
                public string Language { get; set; }
                public TemplateData Template { get; set; }
            }
        }
    }
}