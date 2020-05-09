using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IBuildableComponent : IComponent
    {
        Task<ComponentActionResult> Build();
    }
}