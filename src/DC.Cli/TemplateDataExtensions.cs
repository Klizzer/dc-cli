using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace DC.Cli
{
    public static class TemplateDataExtensions
    {
        private static readonly IImmutableDictionary<
            string,
            Func<
                TemplateData, 
                TemplateData.ResourceData,
                IImmutableDictionary<string, string>, 
                Func<string, string, string>,
                IImmutableDictionary<string, string>>> ResourceEnvironmentHandlers = 
            new Dictionary<
                string, 
                Func<
                    TemplateData, 
                    TemplateData.ResourceData,
                    IImmutableDictionary<string, string>, 
                    Func<string, string, string>,
                    IImmutableDictionary<string, string>>>
            {
                ["AWS::Serverless::Function"] = GetFunctionEnvironmentVariables
            }.ToImmutableDictionary();
        
        public static TemplateData Merge(this IEnumerable<TemplateData> templates)
        {
            var newTemplate = new TemplateData();

            foreach (var template in templates)
                newTemplate.Merge(template);

            return newTemplate;
        }

        public static IImmutableDictionary<string, IImmutableDictionary<string, string>> FindEnvironmentVariables(
            this TemplateData template,
            IImmutableDictionary<string, string> variableValues,
            Func<string, string, string> askForValue)
        {
            var result = new Dictionary<string, IImmutableDictionary<string, string>>();

            foreach (var resource in template.Resources)
            {
                if (ResourceEnvironmentHandlers.ContainsKey(resource.Value.Type))
                {
                    result[resource.Key] = ResourceEnvironmentHandlers[resource.Value.Type](
                        template,
                        resource.Value,
                        variableValues,
                        askForValue);
                }
            }

            return result.ToImmutableDictionary();
        }
        
        private static IImmutableDictionary<string, string> GetFunctionEnvironmentVariables(
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
    }
}