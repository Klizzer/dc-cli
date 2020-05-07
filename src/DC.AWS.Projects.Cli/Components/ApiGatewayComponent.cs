using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class ApiGatewayComponent : IComponent
    {
        public const string ConfigFileName = "api-gw.config.yml";

        private readonly ApiConfiguration _configuration;
        
        private ApiGatewayComponent(DirectoryInfo path, ApiConfiguration configuration)
        {
            Path = path;
            _configuration = configuration;
        }

        public string BaseUrl => _configuration.Settings.BaseUrl;
        public int Port => _configuration.Settings.Port;
        public string Name => _configuration.Name;
        public DirectoryInfo Path { get; }
        
        public static async Task InitAt(
            ProjectSettings settings,
            string path,
            string baseUrl,
            string language,
            int? port = null)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));

            if (Directory.Exists(settings.GetRootedPath(dir.FullName)))
                throw new InvalidOperationException($"You can't add a new api at: \"{dir.FullName}\". It already exists.");

            dir.Create();

            var apiPort = port ?? ProjectSettings.GetRandomUnusedPort();
            
            await Templates.Extract(
                ConfigFileName,
                System.IO.Path.Combine(dir.FullName, ConfigFileName),
                Templates.TemplateType.Infrastructure,
                ("API_NAME", dir.Name),
                ("PORT", apiPort.ToString()),
                ("DEFAULT_LANGUAGE", language),
                ("BASE_URL", baseUrl));
            
            await Templates.Extract(
                "api-gw.make",
                settings.GetRootedPath("services/api-gw.make"),
                Templates.TemplateType.Services,
                false);
        }
        
        public async Task<BuildResult> Build(IBuildContext context)
        {
            var deserializer = new Deserializer();

            var apiConfiguration =
                deserializer.Deserialize<ApiConfiguration>(
                    await File.ReadAllTextAsync(System.IO.Path.Combine(Path.FullName, ConfigFileName)));
            
            context.AddTemplate($"{Path.Name}.api.yml");
            context.ExtendTemplate(apiConfiguration.Settings.Template);
 
            return new BuildResult(true, "");
        }

        public Task<TestResult> Test()
        {
            return Task.FromResult(new TestResult(true, ""));
        }
        
        public ILanguageVersion GetDefaultLanguage(ProjectSettings settings)
        {
            return FunctionLanguage.Parse(_configuration.Settings.DefaultLanguage) ?? settings.GetDefaultLanguage();
        }

        public string GetUrl(string path)
        {
            var url = _configuration.Settings.BaseUrl;
            
            url = url.Trim().TrimStart('/').TrimEnd('/');

            path = path.Trim().TrimStart('/');

            return $"/{url}/{path}";
        }

        public static IEnumerable<ApiGatewayComponent> FindAtPath(DirectoryInfo path)
        {
            if (!File.Exists(System.IO.Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new ApiGatewayComponent(
                path,
                deserializer.Deserialize<ApiConfiguration>(
                    File.ReadAllText(System.IO.Path.Combine(path.FullName, ConfigFileName))));
        }
        
        private class ApiConfiguration
        {
            public string Name { get; set; }
            public ApiSettings Settings { get; set; }
            
            public class ApiSettings
            {
                public int Port { get; set; }
                public string DefaultLanguage { get; set; }
                public string BaseUrl { get; set; }
                public TemplateData Template { get; set; }
            }
        }
    }
}