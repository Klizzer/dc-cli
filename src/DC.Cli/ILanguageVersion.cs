using System.Threading.Tasks;
using DC.Cli.Components;

namespace DC.Cli
{
    public interface ILanguageVersion
    {
        string Language { get; }
        string Version { get; }

        Task<bool> Restore(string path);
        Task<bool> Clean(string path);
        Task<bool> Build(string path);
        Task<bool> Test(string path);
        Task<bool> StartWatch(string path);
        Task<bool> StopWatch(string path);

        string GetHandlerName();
        string GetFunctionOutputPath(string functionPath);
        string GetRuntimeName();
    }
}