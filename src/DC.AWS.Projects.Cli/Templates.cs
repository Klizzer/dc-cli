using System.IO;
using System.Linq;
using System.Reflection;

namespace DC.AWS.Projects.Cli
{
    public static class Templates
    {
        public static void Extract(
            string resourceName,
            string destination,
            TemplateType templateType,
            params (string name, string value)[] variables)
        {
            var directory = Path.GetDirectoryName(destination);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            File.WriteAllText(
                destination, 
                GetContent(resourceName, templateType, variables));
        }

        private static string GetContent(
            string resourceName,
            TemplateType templateType,
            params (string name, string value)[] variables)
        {
            var infrastructureTemplateData = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"DC.AWS.Projects.Cli.Templates.{templateType.ToString()}.{resourceName}");

            using (infrastructureTemplateData)
            using(var reader = new StreamReader(infrastructureTemplateData!))
            {
                var templateData = reader.ReadToEnd();

                templateData = variables
                    .Aggregate(
                        templateData,
                        (current, variable) => current
                            .Replace($"[[{variable.name}]]", variable.value.Trim()));

                return templateData;
            }
        }
        
        public enum TemplateType
        {
            Infrastructure,
            Config,
            Services
        }
    }
}