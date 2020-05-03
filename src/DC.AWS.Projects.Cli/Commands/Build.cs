using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Build
    {
        private static readonly IImmutableDictionary<string, Action<string, TemplateData, TemplateData.ResourceData, ProjectSettings>> TypeHandlers =
            new Dictionary<string, Action<string, TemplateData, TemplateData.ResourceData, ProjectSettings>>
            {
                ["AWS::Serverless::Function"] = HandleFunction
            }.ToImmutableDictionary();
        
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();
            
            var destination = Path.Combine(settings.ProjectRoot, "infrastructure/environment/.generated");

            if (Directory.Exists(destination))
                Directory.Delete(destination, true);

            Directory.CreateDirectory(destination);
            
            var templateData = new TemplateData();
            var apiTemplates = settings
                .Apis
                .ToDictionary(x => x.Key, _ => new TemplateData())
                .ToImmutableDictionary();
            
            var deserializer = new Deserializer();
            
            BuildPath(settings.ProjectRoot, templateData, deserializer, apiTemplates, null);
            
            foreach (var resource in templateData.Resources)
            {
                if (TypeHandlers.ContainsKey(resource.Value.Type))
                    TypeHandlers[resource.Value.Type](resource.Key, templateData, resource.Value, settings);
            }
            
            var serializer = new SerializerBuilder().Build();

            var result = serializer.Serialize(templateData);
            
            File.WriteAllText(Path.Combine(destination, "project.yml"), result);

            foreach (var apiTemplate in apiTemplates)
            {
                File.WriteAllText(
                    Path.Combine(destination, $"{apiTemplate.Key}.api.yml"),
                    serializer.Serialize(apiTemplate.Value));
            }

            foreach (var client in settings.Clients)
                File.WriteAllText(Path.Combine(destination, $"{client.Key}.client"), Json.Serialize(client.Value));
        }

        private static void BuildPath(
            string path,
            TemplateData templateData,
            IDeserializer deserializer,
            IImmutableDictionary<string, TemplateData> apiTemplates,
            string currentApi)
        {
            var dir = new DirectoryInfo(path);

            if (File.Exists(Path.Combine(dir.FullName, "api.infra.yml")))
                currentApi = dir.Name;

            if (File.Exists(Path.Combine(dir.FullName, "function.infra.yml")))
            {
                BuildFunction.Execute(new BuildFunction.Options
                {
                    Path = dir.FullName
                });
            }

            foreach (var file in dir.EnumerateFiles())
            {
                if (!file.Name.EndsWith(".infra.yml")) 
                    continue;
                
                var infraData = deserializer.Deserialize<TemplateData>(File.ReadAllText(file.FullName));
                    
                templateData.Merge(infraData);

                if (apiTemplates.ContainsKey(currentApi ?? ""))
                {
                    apiTemplates[currentApi ?? ""].Merge(infraData);
                }
            }
            
            foreach (var subDir in dir.GetDirectories())
            {
                if (subDir.Name == "node_modules")
                    continue;
                
                BuildPath(subDir.FullName, templateData, deserializer, apiTemplates, currentApi);
            }
        }
        
        private static void HandleFunction(
            string name,
            TemplateData template,
            TemplateData.ResourceData functionNode,
            ProjectSettings settings)
        {
            var variableValuesFileDirectory = Path.Combine(settings.ProjectRoot, ".env");
            
            var environmentVariablesFilePath = Path.Combine(variableValuesFileDirectory, "environment.variables.json");
            
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

            var variableValuesFilePath = Path.Combine(variableValuesFileDirectory, "variables.json");

            var variableValues = new Dictionary<string, string>();
            
            if (File.Exists(variableValuesFilePath))
                variableValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(variableValuesFilePath));

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
                            else if (template.Outputs.ContainsKey(refKey))
                            {
                                resultVariables[variableName] = refKey;
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
                                Console.WriteLine($"Please enter value for environment variable \"{refKey}\" for function {name}:");
                                var parameterValue = Console.ReadLine();

                                resultVariables[variableName] = parameterValue;

                                variableValues[$"{name}.EnvironmentVariable.{refKey}"] = parameterValue;
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

        [Verb("build", HelpText = "Build the project.")]
        public class Options
        {

        }
        
        private class TemplateData
        {
            public string AWSTemplateFormatVersion { get; set; } = "2010-09-09";
            public string Transform { get; set; } = "AWS::Serverless-2016-10-31";
            public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
            public IDictionary<string, IDictionary<string, string>> Parameters { get; set; } = new Dictionary<string, IDictionary<string, string>>();
            public IDictionary<string, ResourceData> Resources { get; set; } = new Dictionary<string, ResourceData>();

            public void Merge(TemplateData other)
            {
                foreach (var output in other.Outputs)
                {
                    if (!Outputs.ContainsKey(output.Key))
                        Outputs[output.Key] = output.Value;
                }

                foreach (var parameter in other.Parameters)
                {
                    if (!Parameters.ContainsKey(parameter.Key))
                        Parameters[parameter.Key] = parameter.Value;
                }
                
                foreach (var resource in other.Resources)
                {
                    if (!Resources.ContainsKey(resource.Key))
                        Resources[resource.Key] = resource.Value;
                }
            }
            
            public class ResourceData
            {
                public string Type { get; set; }
                public IDictionary<string, object> Properties { get; set; }
            }
        }
    }
}