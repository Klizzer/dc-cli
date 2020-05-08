using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IBuildableComponent : IComponent
    {
        Task<ComponentActionResult> Build();
    }
}