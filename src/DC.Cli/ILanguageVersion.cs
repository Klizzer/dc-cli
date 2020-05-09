using System.Threading.Tasks;
using DC.Cli.Components;

namespace DC.Cli
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