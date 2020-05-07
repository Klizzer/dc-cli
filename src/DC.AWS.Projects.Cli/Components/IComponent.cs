using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponent
    {
        string Name { get; }
        Task<ComponentActionResult> Restore();
        Task<ComponentActionResult> Build();
        Task<ComponentActionResult> Test();
        Task<ComponentActionResult> Start(Components.ComponentTree components);
        Task<ComponentActionResult> Stop();
        Task<ComponentActionResult> Logs();
    }
}