using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface ITestableComponent : IComponent
    {
        Task<ComponentActionResult> Test();
    }
}