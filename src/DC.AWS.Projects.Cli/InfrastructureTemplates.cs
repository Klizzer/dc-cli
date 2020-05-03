using System.IO;
using System.Linq;
using System.Reflection;

namespace DC.AWS.Projects.Cli
{
    public static class InfrastructureTemplates
    {
        public static void Extract(
            string resourceName,
            string destination,
            params (string name, string value)[] variables)
        {
            var infrastructureTemplateData = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"DC.AWS.Projects.Cli.Templates.Infrastructure.{resourceName}");

            using (infrastructureTemplateData)
            using(var reader = new StreamReader(infrastructureTemplateData!))
            {
                var templateData = reader.ReadToEnd();

                templateData = variables
                    .Aggregate(
                        templateData,
                        (current, variable) => current
                            .Replace($"[[{variable.name}]]", variable.value));

                File.WriteAllText(destination, templateData);
            }
        }
    }
}