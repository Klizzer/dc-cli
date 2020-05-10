using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface ICloudformationComponent : INeedConfiguration
    {
        Task<TemplateData> GetCloudformationData();
    }
}