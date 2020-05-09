using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface ICloudformationComponent : IComponent
    {
        Task<TemplateData> GetCloudformationData();
    }
}