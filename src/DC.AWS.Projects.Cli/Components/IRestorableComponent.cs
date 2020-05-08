using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IRestorableComponent : IComponent
    {
        Task<ComponentActionResult> Restore();
    }
}