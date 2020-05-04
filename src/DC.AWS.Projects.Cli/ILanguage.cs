using System.Collections.Generic;

namespace DC.AWS.Projects.Cli
{
    public interface ILanguage
    {
        string Name { get; }
        IEnumerable<ILanguageRuntime> GetRuntimes();
        ILanguageRuntime GetDefaultRuntime();
    }
}