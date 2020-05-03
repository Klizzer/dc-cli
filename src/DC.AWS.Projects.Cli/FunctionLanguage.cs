using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DC.AWS.Projects.Cli
{
    public class FunctionLanguage
    {
        private static readonly IImmutableDictionary<string, IImmutableList<string>> AvailableLanguages = 
            new Dictionary<string, IImmutableList<string>>
        {
           ["node"] = new List<string>
           {
               "nodejs10.x",
               "nodejs12.x"
           }.ToImmutableList(),
           ["go"] = new List<string>
           {
               "go1.x"
           }.ToImmutableList()
        }.ToImmutableDictionary();
        
        private FunctionLanguage(string name, string runtime)
        {
            Name = name;
            Runtime = runtime;
        }
        
        public string Name { get; }
        public string Runtime { get; }

        public override string ToString()
        {
            return $"{Name}:{Runtime}";
        }

        public const string DefaultLanguage = "node:nodejs12.x";
        
        public static FunctionLanguage Parse(string language)
        {
            var parts = language.Split(':');

            if (!AvailableLanguages.ContainsKey(parts[0]))
                throw new InvalidOperationException($"We don't support language: {parts[0]}");
            
            if (parts.Length == 1)
                return new FunctionLanguage(parts[0], AvailableLanguages[parts[0]].Last());

            if (!AvailableLanguages[parts[0]].Contains(parts[1]))
            {
                throw new InvalidOperationException(
                    $"We don't support runtime: {parts[1]} for language: {parts[0]}. Available runtimes are: {string.Join(", ", AvailableLanguages[parts[0]])}");
            }
            
            return new FunctionLanguage(parts[0], parts[1]);
        }

        public static FunctionLanguage ParseFromRuntime(string runtime)
        {
            if (!AvailableLanguages.Any(x => x.Value.Contains(runtime)))
                return null;

            var language = AvailableLanguages.First(x => x.Value.Contains(runtime));
            
            return new FunctionLanguage(language.Key, runtime);
        }
    }
}