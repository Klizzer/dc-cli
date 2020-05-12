using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IRestorableComponent : IComponent
    {
        Task<bool> Restore();
    }
}