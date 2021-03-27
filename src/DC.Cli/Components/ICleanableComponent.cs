using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface ICleanableComponent : IComponent
    {
        Task<bool> Clean();
    }
}