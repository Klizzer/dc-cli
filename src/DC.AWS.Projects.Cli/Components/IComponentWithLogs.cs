using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponentWithLogs : IComponent
    {
        Task<ComponentActionResult> Logs();
    }
}