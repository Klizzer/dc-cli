using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Components
{
    public class LambdaFunctionComponent : IComponent
    {
        private const string ConfigFileName = "lambda-func.config.yml";
        
        private static readonly IImmutableDictionary<FunctionTrigger, Func<string, DirectoryInfo, ProjectSettings, Components.ComponentTree, Task<ILanguageVersion>>> TriggerHandlers =
            new Dictionary<FunctionTrigger, Func<string, DirectoryInfo, ProjectSettings, Components.ComponentTree, Task<ILanguageVersion>>>
            {
                [FunctionTrigger.Api] = SetupApiTrigger
            }.ToImmutableDictionary();

        private readonly FunctionConfiguration _configuration;
        
        private LambdaFunctionComponent(DirectoryInfo path, FunctionConfiguration configuration)
        {
            Path = path;
            _configuration = configuration;
        }

        public string Name => _configuration.Name;
        public DirectoryInfo Path { get; }

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
                System.IO.Path.Combine(executingAssembly.GetPath(), $"Templates/Functions/{runtime.Language}"), 
                path);
        }

        public Task<RestoreResult> Restore()
        {
            var languageVersion = _configuration.GetLanguage();

            return languageVersion.Restore(Path.FullName);
        }

        public async Task<BuildResult> Build(IBuildContext context)
        {
            var infraData = await LoadTemplate();
            
            context.ExtendTemplate(infraData);

            var functionResources = FindAllFunctions(infraData);

            foreach (var functionResource in functionResources)
            {
                HandleFunctionTemplate(
                    functionResource.Key,
                    infraData,
                    functionResource.Value,
                    context.ProjectSettings);
            }

            var languageVersion = _configuration.GetLanguage();

            return await languageVersion.Build(Path.FullName);
        }

        public Task<TestResult> Test()
        {
            var languageVersion = _configuration.GetLanguage();

            return languageVersion.Test(Path.FullName);
        }

        private async Task<TemplateData> LoadTemplate()
        {
            var deserializer = new Deserializer();

            var functionConfig = deserializer.Deserialize<FunctionConfiguration>(
                await File.ReadAllTextAsync(System.IO.Path.Combine(Path.FullName, ConfigFileName)));

            return functionConfig.Settings.Template;
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
                settings.GetRootedPath(System.IO.Path.Combine(path.FullName, ConfigFileName)),
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
        
        private static IImmutableDictionary<string, TemplateData.ResourceData> FindAllFunctions(TemplateData templateData)
        {
            return templateData
                .Resources
                .Where(x => x.Value.Type == "AWS::Serverless::Function")
                .ToImmutableDictionary(x => x.Key, x => x.Value);
        }
        
        private static void HandleFunctionTemplate(
            string name,
            TemplateData template,
            TemplateData.ResourceData functionNode,
            ProjectSettings settings)
        {
            var variableValuesFileDirectory = System.IO.Path.Combine(settings.ProjectRoot, ".env");
            
            var environmentVariablesFilePath = System.IO.Path.Combine(variableValuesFileDirectory, "environment.variables.json");
            
            if (!Directory.Exists(variableValuesFileDirectory))
                Directory.CreateDirectory(variableValuesFileDirectory);
            
            if (!File.Exists(environmentVariablesFilePath))
                File.WriteAllText(environmentVariablesFilePath, "{}");
            
            if (!functionNode.Properties.ContainsKey("Environment") ||
                !((IDictionary<object, object>) functionNode.Properties["Environment"]).ContainsKey("Variables"))
            {
                return;
            }

            var variables =
                (IDictionary<object, object>) ((IDictionary<object, object>) functionNode.Properties["Environment"])[
                    "Variables"];
            
            var resultVariables = new Dictionary<string, string>();

            var variableValuesFilePath = System.IO.Path.Combine(variableValuesFileDirectory, "variables.json");

            var variableValues = new Dictionary<string, string>();
            
            if (File.Exists(variableValuesFilePath))
                variableValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(variableValuesFilePath));

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

                            if (variableValues.ContainsKey($"{name}.EnvironmentVariable.{refKey}"))
                            {
                                resultVariables[variableName] =
                                    variableValues[$"{name}.EnvironmentVariable.{refKey}"];
                            }
                            else if (template.Parameters.ContainsKey(refKey) &&
                                     variableValues.ContainsKey($"Variable.{refKey}"))
                            {
                                resultVariables[variableName] = variableValues[$"Variable.{refKey}"];
                            }
                            else if (template.Parameters.ContainsKey(refKey) &&
                                     template.Parameters[refKey].ContainsKey("Default"))
                            {
                                resultVariables[variableName] = template.Parameters[refKey]["Default"];
                            }
                            else if (template.Parameters.ContainsKey(refKey))
                            {
                                Console.WriteLine($"Please enter value for parameter \"{refKey}\":");
                                var parameterValue = Console.ReadLine();

                                resultVariables[variableName] = parameterValue;

                                variableValues[$"Variable.{refKey}"] = parameterValue;
                            }
                            else
                            {
                                resultVariables[variableName] = refKey;
                            }
                        }
                        break;
                }
            }
            
            File.WriteAllText(variableValuesFilePath, JsonConvert.SerializeObject(variableValues));

            var currentEnvironmentVariables =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
                    File.ReadAllText(environmentVariablesFilePath));

            currentEnvironmentVariables[name] = resultVariables;
            
            File.WriteAllText(environmentVariablesFilePath, JsonConvert.SerializeObject(currentEnvironmentVariables));
        }
        
        public static IEnumerable<LambdaFunctionComponent> FindAtPath(DirectoryInfo path)
        {
            if (!File.Exists(System.IO.Path.Combine(path.FullName, ConfigFileName))) 
                yield break;
            
            var deserializer = new Deserializer();
            yield return new LambdaFunctionComponent(
                path,
                deserializer.Deserialize<FunctionConfiguration>(
                    File.ReadAllText(System.IO.Path.Combine(path.FullName, ConfigFileName))));
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