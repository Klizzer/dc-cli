using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.LambdaFunction
{
    public class LambdaFunctionComponent : ICloudformationComponent,
        IRestorableComponent,
        IBuildableComponent,
        ITestableComponent,
        IStartableComponent
    {
        public const string ConfigFileName = "lambda-func.config.yml";
        
        private readonly DirectoryInfo _path;
        private readonly FunctionConfiguration _configuration;
        private readonly ProjectSettings _settings;
        
        private LambdaFunctionComponent(
            DirectoryInfo path,
            FunctionConfiguration configuration,
            ProjectSettings settings)
        {
            _path = path;
            _configuration = configuration;
            _settings = settings;
        }

        public string Name => _configuration.Name;
        
        public Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>> 
            GetRequiredConfigurations()
        {
            return _configuration.Settings.Template.GetRequiredConfigurations();
        }

        public Task<bool> Restore()
        {
            return _configuration.GetLanguage().Restore(_path.FullName);
        }

        public Task<bool> Build()
        {
            return _configuration.GetLanguage().Build(_path.FullName);
        }

        public Task<bool> Test()
        {
            return _configuration.GetLanguage().Test(_path.FullName);
        }
        
        public Task<bool> Start(Components.ComponentTree components)
        {
            //TODO: Setup watch
            return Build();
        }

        public Task<bool> Stop()
        {
            return Task.FromResult(true);
        }
        
        public Task<TemplateData> GetCloudformationData()
        {
            var template = _configuration.Settings.Template;
            
            var functionPath = _settings.GetRelativePath(_path.FullName);
            var languageVersion = _configuration.GetLanguage();
            
            return Task.FromResult(template.SetCodeUris(languageVersion.GetFunctionOutputPath(functionPath)));
        }
        
        public static async Task<LambdaFunctionComponent> Init(DirectoryInfo path, ProjectSettings settings)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                return null;
            
            var deserializer = new Deserializer();
            return new LambdaFunctionComponent(
                path,
                deserializer.Deserialize<FunctionConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName))),
                settings);
        }
        
        private class FunctionConfiguration
        {
            public string Name { get; set; }
            public FunctionSettings Settings { get; set; }

            public ILanguageVersion GetLanguage()
            {
                return FunctionLanguage.Parse(Settings.Language);
            }
            
            public class FunctionSettings
            {
                public string Type { get; set; }
                public string Language { get; set; }
                public TemplateData Template { get; set; }
            }
        }
    }
}