using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class LambdaFunctionComponent : ICloudformationComponent, 
        ISupplyCloudformationEnvironmentVariables,
        IRestorableComponent,
        IBuildableComponent,
        ITestableComponent
    {
        private const string ConfigFileName = "lambda-func.config.yml";
        
        private static readonly IImmutableDictionary<FunctionTrigger, Func<string, DirectoryInfo, ProjectSettings, Components.ComponentTree, Task<ILanguageVersion>>> TriggerHandlers =
            new Dictionary<FunctionTrigger, Func<string, DirectoryInfo, ProjectSettings, Components.ComponentTree, Task<ILanguageVersion>>>
            {
                [FunctionTrigger.Api] = SetupApiTrigger
            }.ToImmutableDictionary();

        private readonly DirectoryInfo _path;
        private readonly FunctionConfiguration _configuration;
        
        private LambdaFunctionComponent(DirectoryInfo path, FunctionConfiguration configuration)
        {
            _path = path;
            _configuration = configuration;
        }

        public string Name => _configuration.Name;

        public static async Task InitAt(ProjectSettings settings,
            FunctionTrigger trigger,
            string language,
            string path,
            Components.ComponentTree componentTree)
        {
            if (Directory.Exists(path))
                throw new InvalidOperationException($"You can't add a new function at: \"{path}\". It already exists.");
                
            var dir = Directory.CreateDirectory(path);

            var runtime = await TriggerHandlers[trigger](language, dir, settings, componentTree);
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            await Directories.Copy(
                Path.Combine(executingAssembly.GetPath(), $"Templates/Functions/{runtime.Language}"), 
                path);
        }

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

        public IImmutableDictionary<string, IImmutableDictionary<string, string>> GetResourceEnvironmentVariables(
            ProjectSettings settings,
            IImmutableDictionary<string, string> variableValues,
            Func<string, string, string> askForValue)
        {
            var result = new Dictionary<string, IImmutableDictionary<string, string>>();
            
            foreach (var functionResource in _configuration.Settings.Template.Resources.Where(x => x.Value.Type == "AWS::Serverless::Function"))
            {
                var variables = GetFunctionEnvironmentVariables(
                    functionResource.Key,
                    _configuration.Settings.Template,
                    functionResource.Value,
                    variableValues,
                    askForValue);

                result[functionResource.Key] = variables;
            }

            return result.ToImmutableDictionary();
        }

        public Task<TemplateData> GetCloudformationData()
        {
            return Task.FromResult(_configuration.Settings.Template);
        }

        private static async Task<ILanguageVersion> SetupApiTrigger(
            string language,
            DirectoryInfo path,
            ProjectSettings settings,
            Components.ComponentTree componentTree)
        {
            var apiComponent = componentTree.FindFirst<ApiGatewayComponent>(Components.Direction.Out);
            
            if (apiComponent == null)
                throw new InvalidOperationException("Can't add a api-function outside of any api.");
            
            var functionPath = settings.GetRelativePath(path.FullName);
            var languageVersion = FunctionLanguage.Parse(language) ?? apiComponent.GetDefaultLanguage(settings);

            Console.WriteLine("Enter url:");
            var url = apiComponent.GetUrl(Console.ReadLine());

            Console.WriteLine("Enter method:");
            var method = Console.ReadLine();
            
            await Templates.Extract(
                "api-lambda-function.config.yml",
                settings.GetRootedPath(Path.Combine(path.FullName, ConfigFileName)),
                Templates.TemplateType.Infrastructure,
                ("FUNCTION_NAME", path.Name),
                ("FUNCTION_TYPE", "api"),
                ("LANGUAGE", languageVersion.ToString()),
                ("FUNCTION_RUNTIME", languageVersion.GetRuntimeName()),
                ("FUNCTION_METHOD", method),
                ("FUNCTION_PATH", languageVersion.GetFunctionOutputPath(functionPath)),
                ("API_NAME", apiComponent.Name),
                ("URL", url),
                ("FUNCTION_HANDLER", languageVersion.GetHandlerName()));

            return languageVersion;
        }
        
        private static IImmutableDictionary<string, string> GetFunctionEnvironmentVariables(
            string name,
            TemplateData template,
            TemplateData.ResourceData functionNode,
            IImmutableDictionary<string, string> variableValues,
            Func<string, string, string> askForValue)
        {
            if (!functionNode.Properties.ContainsKey("Environment") ||
                !((IDictionary<object, object>) functionNode.Properties["Environment"]).ContainsKey("Variables"))
            {
                return new Dictionary<string, string>().ToImmutableDictionary();
            }

            var variables =
                (IDictionary<object, object>) ((IDictionary<object, object>) functionNode.Properties["Environment"])[
                    "Variables"];
            
            var resultVariables = new Dictionary<string, string>();

            //TODO: Refactor
            foreach (var variable in variables)
            {
                var variableName = variable.Key.ToString() ?? "";
                
                switch (variable.Value)
                {
                    case string variableValue:
                        resultVariables[variableName] = variableValue;
                        break;
                    case IDictionary<object, object> objectVariableValue:
                        if (objectVariableValue.ContainsKey("Ref"))
                        {
                            var refKey = objectVariableValue["Ref"].ToString() ?? "";

                            if (template.Parameters.ContainsKey(refKey) &&
                                     variableValues.ContainsKey(refKey))
                            {
                                resultVariables[variableName] = variableValues[refKey];
                            }
                            else if (template.Parameters.ContainsKey(refKey) &&
                                     template.Parameters[refKey].ContainsKey("Default"))
                            {
                                resultVariables[variableName] = template.Parameters[refKey]["Default"];
                            }
                            else if (template.Parameters.ContainsKey(refKey))
                            {
                                resultVariables[variableName] =
                                    askForValue($"Please enter value for parameter \"{refKey}\":", variableName);
                            }
                            else
                            {
                                resultVariables[variableName] = refKey;
                            }
                        }
                        break;
                }
            }

            return resultVariables.ToImmutableDictionary();
        }
        
        public static IEnumerable<LambdaFunctionComponent> FindAtPath(DirectoryInfo path)
        {
            if (!File.Exists(Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new LambdaFunctionComponent(
                path,
                deserializer.Deserialize<FunctionConfiguration>(
                    File.ReadAllText(Path.Combine(path.FullName, ConfigFileName))));
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