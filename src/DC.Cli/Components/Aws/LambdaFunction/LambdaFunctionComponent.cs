using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.Cli.Components.Aws.LambdaFunction
{
    public class LambdaFunctionComponent : ICloudformationComponent,
        IRestorableComponent,
        IBuildableComponent,
        ITestableComponent
    {
        public const string ConfigFileName = "lambda-func.config.yml";
        
        private readonly DirectoryInfo _path;
        private readonly FunctionConfiguration _configuration;
        
        private LambdaFunctionComponent(DirectoryInfo path, FunctionConfiguration configuration)
        {
            _path = path;
            _configuration = configuration;
        }

        public string Name => _configuration.Name;
        
        public Task<ComponentActionResult> Restore()
        {
            return _configuration.GetLanguage().Restore(_path.FullName);
        }

        public async Task<ComponentActionResult> Build()
        {
            return await _configuration.GetLanguage().Build(_path.FullName);
        }

        public Task<ComponentActionResult> Test()
        {
            return _configuration.GetLanguage().Test(_path.FullName);
        }
        
        public Task<TemplateData> GetCloudformationData()
        {
            return Task.FromResult(_configuration.Settings.Template);
        }
        
        public static async Task<LambdaFunctionComponent> Init(DirectoryInfo path)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                return null;
            
            var deserializer = new Deserializer();
            return new LambdaFunctionComponent(
                path,
                deserializer.Deserialize<FunctionConfiguration>(
                    await File.ReadAllTextAsync(Path.Combine(path.FullName, ConfigFileName))));
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