using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IStartableComponent : IComponent
    {
        Task<bool> Start(Components.ComponentTree components);
        Task<bool> Stop();
    }
}