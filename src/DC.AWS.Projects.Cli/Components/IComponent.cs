using System.IO;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponent
    {
        string Name { get; }
        DirectoryInfo Path { get; }
        Task<RestoreResult> Restore();
        Task<BuildResult> Build(IBuildContext context);
        Task<TestResult> Test();
    }
}