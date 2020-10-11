using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DC.Cli.Components.Aws.ApiGateway;

namespace DC.Cli.Components.Aws.LambdaFunction
{
    public class LambdaFunctionComponentType 
        : IComponentType<LambdaFunctionComponent, LambdaFunctionComponentType.ComponentData>
    {
        private static readonly IImmutableDictionary<FunctionTrigger, Func<string, DirectoryInfo, ProjectSettings, Components.ComponentTree, string, Task<ILanguageVersion>>> TriggerHandlers =
            new Dictionary<FunctionTrigger, Func<string, DirectoryInfo, ProjectSettings, Components.ComponentTree, string, Task<ILanguageVersion>>>
            {
                [FunctionTrigger.Api] = SetupApiTrigger
            }.ToImmutableDictionary();

        public async Task<LambdaFunctionComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            var configFilePath = Path.Combine(tree.Path.FullName, LambdaFunctionComponent.ConfigFileName);
            
            if (File.Exists(configFilePath))
                throw new InvalidOperationException($"You can't add a new function at: \"{tree.Path.FullName}\". It already exists.");

            var languageVersion = await TriggerHandlers[data.Trigger](data.Language, tree.Path, settings, tree, data.Name);
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            await Directories.Copy(
                Path.Combine(executingAssembly.GetPath(), $"Templates/Functions/{languageVersion.Language}"), 
                tree.Path.FullName);

            return await LambdaFunctionComponent.Init(tree.Path, settings);
        }

        public async Task<IImmutableList<IComponent>> FindAt(
            Components.ComponentTree components,
            ProjectSettings settings)
        {
            var component = await LambdaFunctionComponent.Init(components.Path, settings);

            return component != null
                ? new List<IComponent>
                {
                    component
                }.ToImmutableList()
                : ImmutableList<IComponent>.Empty;
        }
        
        private static async Task<ILanguageVersion> SetupApiTrigger(
            string language,
            DirectoryInfo path,
            ProjectSettings settings,
            Components.ComponentTree componentTree,
            string name)
        {
            var apiComponent = componentTree.FindFirst<ApiGatewayComponent>(Components.Direction.Out);
            
            if (apiComponent == null)
                throw new InvalidOperationException("Can't add a api-function outside of any api.");
            
            var languageVersion = FunctionLanguage.Parse(language) ?? apiComponent.GetDefaultLanguage(settings);

            Console.WriteLine("Enter url:");
            var url = apiComponent.GetUrl(Console.ReadLine());

            Console.WriteLine("Enter method:");
            var method = Console.ReadLine();
            
            await Templates.Extract(
                "api-lambda-function.config.yml",
                settings.GetRootedPath(Path.Combine(path.FullName, LambdaFunctionComponent.ConfigFileName)),
                Templates.TemplateType.Infrastructure,
                ("NAME", name),
                ("FUNCTION_NAME", TemplateData.SanitizeResourceName(name)),
                ("FUNCTION_TYPE", "api"),
                ("LANGUAGE", languageVersion.ToString()),
                ("FUNCTION_RUNTIME", languageVersion.GetRuntimeName()),
                ("FUNCTION_METHOD", method),
                ("API_NAME", TemplateData.SanitizeResourceName(apiComponent.Name)),
                ("URL", url),
                ("FUNCTION_HANDLER", languageVersion.GetHandlerName()));

            return languageVersion;
        }

        public class ComponentData
        {
            public ComponentData(string name, FunctionTrigger trigger, string language)
            {
                Name = name;
                Trigger = trigger;
                Language = language;
            }

            public string Name { get; }
            public FunctionTrigger Trigger { get; }
            public string Language { get; }
        }
    }
}