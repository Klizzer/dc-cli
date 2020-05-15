using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IParseCloudformationValues : IComponent
    {
        Task<object> Parse(object value, TemplateData template = null);
    }
}