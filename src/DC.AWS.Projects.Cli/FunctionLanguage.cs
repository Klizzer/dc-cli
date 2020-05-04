using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DC.AWS.Projects.Cli
{
    public static class FunctionLanguage
    {
        private static readonly IImmutableList<ILanguage> AvailableLanguages = new List<ILanguage>
        {
            NodeLanguage.Instance,
            GoLanguage.Instance
        }.ToImmutableList();
        
        public const string DefaultLanguage = NodeLanguage.LanguageName;
        
        public static ILanguageRuntime Parse(string language)
        {
            if (string.IsNullOrEmpty(language))
                return null;
            
            var parts = language.Split(':');

            var availableLanguage = AvailableLanguages.FirstOrDefault(x => x.Name == parts[0]);

            if (availableLanguage == null)
                throw new InvalidOperationException($"We don't support language: {parts[0]}");

            if (parts.Length == 1)
                return availableLanguage.GetDefaultRuntime();

            var availableRuntimes = availableLanguage.GetRuntimes().ToImmutableList();

            var availableRuntime = availableRuntimes.FirstOrDefault(x => x.Name == parts[1]);

            if (availableRuntime != null)
                return availableRuntime;

            throw new InvalidOperationException(
                $"We don't support runtime: {parts[1]} for language: {parts[0]}. Available runtimes are: {string.Join(", ", availableRuntimes.Select(x => x.Name))}");
        }

        public static ILanguageRuntime ParseFromRuntime(string runtime)
        {
            return AvailableLanguages
                .SelectMany(x => x.GetRuntimes())
                .FirstOrDefault(x => x.Name == runtime);
        }
    }
}