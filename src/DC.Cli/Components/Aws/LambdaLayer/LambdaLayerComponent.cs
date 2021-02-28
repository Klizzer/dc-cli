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
        public const string ConfigFileName = "lambda-layer.config.yml";
        
        private readonly DirectoryInfo _path;
        private readonly LambdaLayerConfiguration _configuration;
        private readonly ProjectSettings _settings;
        
        private LambdaLayerComponent(
            DirectoryInfo path,
            LambdaLayerConfiguration configuration,
            ProjectSettings settings)
        {
            _path = path;
            _configuration = configuration;
            _settings = settings;
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
            GetRequiredConfigurations(Components.ComponentTree components)
        {
            return _configuration.Settings.Template.GetRequiredConfigurations();
        }

        public Task<TemplateData> GetCloudformationData(Components.ComponentTree components)
        {
            var template = _configuration.Settings.Template;

            return Task.FromResult(template.SetContentUris(_settings.GetRelativePath(_path.Parent?.FullName)));
        }
        
        public static async Task<LambdaLayerComponent> Init(DirectoryInfo path, ProjectSettings settings)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                return null;
            
            var deserializer = new Deserializer();
            return new LambdaLayerComponent(
                path,
                deserializer.Deserialize<LambdaLayerConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName))),
                settings);
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