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

        public const string DefaultLanguageConfigurationKay = "defaultFunctionLanguage";
        
        public static ILanguageVersion Parse(string language)
        {
            if (string.IsNullOrEmpty(language))
                return null;
            
            var parts = language.Split(':');

            var availableLanguage = AvailableLanguages.FirstOrDefault(x => x.Name == parts[0]);

            if (availableLanguage == null)
                throw new InvalidOperationException($"We don't support language: {parts[0]}");

            if (parts.Length == 1)
                return availableLanguage.GetDefaultVersion();

            var availableVersions = availableLanguage.GetVersions().ToImmutableList();

            var availableVersion = availableVersions.FirstOrDefault(x => x.Version == parts[1]);

            if (availableVersion != null)
                return availableVersion;

            throw new InvalidOperationException(
                $"We don't support version: {parts[1]} for language: {parts[0]}. Available versions are: {string.Join(", ", availableVersions.Select(x => x.Version))}");
        }

        public static ILanguageVersion GetLanguage(params string[] prioOrder)
        {
            foreach (var languageName in prioOrder)
            {
                var languageVersion = Parse(languageName);

                if (languageVersion != null)
                    return languageVersion;
            }

            return NodeLanguage.Instance.GetDefaultVersion();
        }
    }
}