using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli
{
    public interface ILanguageRuntime
    {
        string Language { get; }
        string Name { get; }

        Task<BuildResult> Build(string path);
        Task<TestResult> Test(string path);

        string GetHandlerName();
        string GetFunctionOutputPath(string functionPath);
    }
}