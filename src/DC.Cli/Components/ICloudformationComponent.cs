using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface ICloudformationComponent : IComponent
    {
        Task<TemplateData> GetCloudformationData();
    }
}