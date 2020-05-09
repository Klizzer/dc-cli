using System.Collections.Generic;

namespace DC.Cli
{
    public interface ILanguage
    {
        string Name { get; }
        IEnumerable<ILanguageVersion> GetVersions();
        ILanguageVersion GetDefaultVersion();
    }
}