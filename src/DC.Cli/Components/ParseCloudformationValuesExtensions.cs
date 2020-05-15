using System.Collections.Generic;
using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public static class ParseCloudformationValuesExtensions
    {
        public static async Task<object> Parse(
            this IEnumerable<IParseCloudformationValues> parsers,
            object value,
            TemplateData template = null)
        {
            foreach (var parser in parsers)
            {
                var parsedValue = await parser.Parse(value, template);

                if (parsedValue != null)
                    return parsedValue;
            }

            return null;
        }
    }
}