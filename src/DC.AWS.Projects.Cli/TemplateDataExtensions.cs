using System.Collections.Generic;

namespace DC.AWS.Projects.Cli
{
    public static class TemplateDataExtensions
    {
        public static TemplateData Merge(this IEnumerable<TemplateData> templates)
        {
            var newTemplate = new TemplateData();

            foreach (var template in templates)
                newTemplate.Merge(template);

            return newTemplate;
        }
    }
}