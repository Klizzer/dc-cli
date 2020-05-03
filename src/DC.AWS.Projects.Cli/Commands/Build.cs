using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CommandLine;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Build
    {
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

        [Verb("build", HelpText = "Build the project.")]
        public class Options
        {

        }
        
        private class TemplateData
        {
            public string AWSTemplateFormatVersion { get; set; } = "2010-09-09";
            public string Transform { get; set; } = "AWS::Serverless-2016-10-31";
            public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
            public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
            public IDictionary<string, object> Resources { get; set; } = new Dictionary<string, object>();

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
        }
    }
}