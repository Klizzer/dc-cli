using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DC.AWS.Projects.Cli
{
    public class GoLanguage : ILanguage
    {
        public const string LanguageName = "go";

        public string Name { get; } = LanguageName;
        
        public static ILanguage Instance { get; } = new GoLanguage();
        
        private static ILanguageRuntime Go1 { get; } = new GoRuntime("go1.x");
        
        public IEnumerable<ILanguageRuntime> GetRuntimes()
        {
            yield return Go1;
        }

        public ILanguageRuntime GetDefaultRuntime()
        {
            return Go1;
        }
        
        private class GoRuntime : ILanguageRuntime
        {
            public GoRuntime(string name)
            {
                Name = name;
            }

            public string Language { get; } = LanguageName;
            public string Name { get; }
            
            public void Build(string path)
            {
                var restoreProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "go",
                    Arguments = "get -v -t -d ./...",
                    WorkingDirectory = path
                });

                restoreProcess?.WaitForExit();

                var buildProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "go",
                    Arguments = "build -o ./.out/main -v .",
                    WorkingDirectory = path
                });

                buildProcess?.WaitForExit();
            }

            public string GetHandlerName()
            {
                return "main";
            }

            public string GetFunctionOutputPath(string functionPath)
            {
                return Path.Combine(functionPath, ".out");
            }
        }
    }
}