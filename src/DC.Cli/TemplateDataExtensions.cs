using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using DC.Cli.Components;

namespace DC.Cli
{
    public static class TemplateDataExtensions
    {
        public static TemplateData Merge(
            this IEnumerable<TemplateData> templates)
        {
            var newTemplate = new TemplateData();

            foreach (var template in templates)
                newTemplate.Merge(template);

            return newTemplate;
        }

        public static TemplateData SetCodeUris(this TemplateData template, string codeUri)
        {
            if (template.Resources == null)
                return template;
            
            foreach (var resource in template.Resources.Where(x => x.Value?.Type == "AWS::Serverless::Function"))
            {
                if (resource.Value.Properties == null) 
                    continue;
                    
                if (!resource.Value.Properties.ContainsKey("CodeUri") ||
                    resource.Value.Properties["CodeUri"] == null)
                {
                    resource.Value.Properties["CodeUri"] = codeUri;
                }
            }

            return template;
        }

        public static TemplateData SetContentUris(this TemplateData template, string contentUri)
        {
            if (template.Resources == null)
                return template;
            
            foreach (var resource in template.Resources.Where(x => x.Value?.Type == "AWS::Serverless::LayerVersion"))
            {
                if (resource.Value.Properties == null) 
                    continue;
                    
                if (!resource.Value.Properties.ContainsKey("ContentUri") ||
                    resource.Value.Properties["ContentUri"] == null)
                {
                    resource.Value.Properties["ContentUri"] = contentUri;
                }
            }

            return template;
        }

        public static Task<IEnumerable<(string key, string question, INeedConfiguration.ConfigurationType configurationType)>>
            GetRequiredConfigurations(this TemplateData templateData)
        {
            return Task.FromResult(templateData.Parameters.Select(parameter => ($"{SettingNamespaces.CloudformationParameters}{parameter.Key}",
                $"Please enter local value for cloudformation parameter: \"{parameter.Key}\"",
                INeedConfiguration.ConfigurationType.Project)));
        }

        public static async Task<IImmutableDictionary<string, IImmutableDictionary<string, string>>> FindEnvironmentVariables(
            this TemplateData template,
            Components.Components.ComponentTree components)
        {
            var result = new Dictionary<string, IImmutableDictionary<string, string>>();

            foreach (var resource in template.Resources)
            {
                if (resource.Value.Type == "AWS::Serverless::Function")
                {
                    result[resource.Key] = await GetFunctionEnvironmentVariables(
                        template,
                        resource.Value,
                        components);
                }
            }

            return result.ToImmutableDictionary();
        }
        
        private static async Task<IImmutableDictionary<string, string>> GetFunctionEnvironmentVariables(
            TemplateData template,
            TemplateData.ResourceData functionNode,
            Components.Components.ComponentTree components)
        {
            if (!functionNode.Properties.ContainsKey("Environment") ||
                !((IDictionary<object, object>) functionNode.Properties["Environment"]).ContainsKey("Variables"))
            {
                return new Dictionary<string, string>().ToImmutableDictionary();
            }

            var parsers = components
                .FindAll<IParseCloudformationValues>(Components.Components.Direction.Out)
                .Select(x => x.component)
                .ToImmutableList();

            var variables =
                (IDictionary<object, object>) ((IDictionary<object, object>) functionNode.Properties["Environment"])[
                    "Variables"];
            
            var resultVariables = new Dictionary<string, string>();

            foreach (var variable in variables)
            {
                var variableName = variable.Key.ToString() ?? "";

                var parsedValue = await parsers.Parse(variable.Value, template); 

                resultVariables[variableName] = (parsedValue ?? "").ToString();
            }

            return resultVariables.ToImmutableDictionary();
        }
    }
}