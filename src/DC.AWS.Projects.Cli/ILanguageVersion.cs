using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli
{
    public interface ILanguageVersion
    {
        string Language { get; }
        string Version { get; }

        Task<BuildResult> Build(string path);
        Task<TestResult> Test(string path);

        string GetHandlerName();
        string GetFunctionOutputPath(string functionPath);
        string GetRuntimeName();
    }
}