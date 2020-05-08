using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli
{
    public interface ILanguageVersion
    {
        string Language { get; }
        string Version { get; }

        Task<ComponentActionResult> Restore(string path);
        Task<ComponentActionResult> Build(string path);
        Task<ComponentActionResult> Test(string path);

        string GetHandlerName();
        string GetFunctionOutputPath(string functionPath);
        string GetRuntimeName();
    }
}