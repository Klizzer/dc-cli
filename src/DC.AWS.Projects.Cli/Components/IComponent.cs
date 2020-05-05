using System.IO;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponent
    {
        DirectoryInfo Path { get; }
        Task<BuildResult> Build(IBuildContext context);
        Task<TestResult> Test();
    }
}