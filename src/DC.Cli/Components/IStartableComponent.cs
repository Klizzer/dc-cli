using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IStartableComponent : IComponent
    {
        Task<ComponentActionResult> Start(Components.ComponentTree components);
        Task<ComponentActionResult> Stop();
    }
}