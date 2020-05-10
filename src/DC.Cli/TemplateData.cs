using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DC.Cli
{
    public class TemplateData
    {
        public string AWSTemplateFormatVersion { get; set; } = "2010-09-09";
        public string Transform { get; set; } = "AWS::Serverless-2016-10-31";
        public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
        public IDictionary<string, IDictionary<string, string>> Parameters { get; set; } = new Dictionary<string, IDictionary<string, string>>();
        public IDictionary<string, ResourceData> Resources { get; set; } = new Dictionary<string, ResourceData>();

        public void Merge(TemplateData other, params string[] servicesToInclude)
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
                if (!Resources.ContainsKey(resource.Key)
                    && (!servicesToInclude.Any() || servicesToInclude.Contains(resource.Value.Type)))
                {
                    Resources[resource.Key] = resource.Value;
                }
            }
        }

        public static string SanitizeResourceName(string name)
        {
            return Regex.Replace(name, "[^a-zA-Z\\s]+", "");
        }
            
        public class ResourceData
        {
            public string Type { get; set; }
            public IDictionary<string, object> Properties { get; set; }
        }
    }
}