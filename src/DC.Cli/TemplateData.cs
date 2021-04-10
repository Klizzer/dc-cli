using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DC.Cli
{
    public class TemplateData
    {
        public string AWSTemplateFormatVersion { get; set; } = "2010-09-09";
        public string Transform { get; set; } = "AWS::Serverless-2016-10-31";

        public IDictionary<string, object> Globals { get; set; } = new Dictionary<string, object>();
        public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
        public IDictionary<string, IDictionary<string, object>> Parameters { get; set; } = new Dictionary<string, IDictionary<string, object>>();
        public IDictionary<string, ResourceData> Resources { get; set; } = new Dictionary<string, ResourceData>();

        public void Merge(TemplateData other)
        {
            foreach (var global in other.Globals)
            {
                if (!Globals.ContainsKey(global.Key))
                    Globals[global.Key] = global.Value;
            }
            
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
                if (Resources.ContainsKey(resource.Key))
                    Resources[resource.Key] = Resources[resource.Key].Merge(resource.Value);
                else
                    Resources[resource.Key] = resource.Value;
            }
        }

        public static string SanitizeResourceName(string name)
        {
            return Regex.Replace(name, "[^a-zA-Z\\s]+", "");
        }
            
        public class ResourceData
        {
            public string Type { get; set; }
            public IList<string> DependsOn { get; set; } = new List<string>();
            public IDictionary<string, object> Properties { get; set; }

            public ResourceData Merge(ResourceData other)
            {
                var dependsOn = new List<string>();
                
                if (DependsOn != null)
                    dependsOn.AddRange(DependsOn);
                
                if (other.DependsOn != null)
                    dependsOn.AddRange(other.DependsOn);

                dependsOn = dependsOn.Distinct().ToList();
                
                return new ResourceData
                {
                    Type = Type,
                    DependsOn = dependsOn,
                    Properties = Merge(Properties, other.Properties)
                };
            }

            private static IDictionary<string, object> Merge(
                IDictionary<string, object> first,
                IDictionary<string, object> second)
            {
                var result = new Dictionary<string, object>();

                foreach (var item in first)
                {
                    if (!second.ContainsKey(item.Key))
                        result[item.Key] = item.Value;
                    else
                    {
                        var firstValue = first[item.Key];
                        var secondValue = second[item.Key];

                        if (firstValue is IDictionary<string, object> firstProps &&
                            secondValue is IDictionary<string, object> secondProps)
                        {
                            result[item.Key] = Merge(firstProps, secondProps);
                        }
                        else
                        {
                            result[item.Key] = item.Value;
                        }
                    }
                }

                foreach (var item in second.Where(x => !result.ContainsKey(x.Key)))
                {
                    result[item.Key] = item.Value;
                }

                return result;
            }
        }
    }
}