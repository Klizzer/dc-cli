using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IComponentWithLogs : IComponent
    {
        Task<bool> Logs();
    }
}