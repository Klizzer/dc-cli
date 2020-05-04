using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DC.AWS.Projects.Cli
{
    public class NodeLanguage : ILanguage
    {
        public const string LanguageName = "node";

        private NodeLanguage()
        {
            
        }

        public static ILanguage Instance { get; } = new NodeLanguage();
        private static ILanguageRuntime Node10 { get; } = new NodeRuntime("nodejs10.x");
        private static ILanguageRuntime Node12 { get; } = new NodeRuntime("nodejs12.x");

        public string Name { get; } = LanguageName;

        public IEnumerable<ILanguageRuntime> GetRuntimes()
        {
            yield return Node10;
            yield return Node12;
        }

        public ILanguageRuntime GetDefaultRuntime()
        {
            return Node12;
        }

        private class NodeRuntime : ILanguageRuntime
        {
            public NodeRuntime(string runtime)
            {
                Name = runtime;
            }

            public string Language { get; } = LanguageName;
            public string Name { get; }
            
            public void Build(string path)
            {
                if (!File.Exists(Path.Combine(path, "package.json")))
                    return;
            
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "yarn",
                    WorkingDirectory = path
                });

                process?.WaitForExit();    
            }

            public string GetHandlerName()
            {
                return "handler.handler";
            }

            public string GetFunctionOutputPath(string functionPath)
            {
                return functionPath;
            }
        }
    }
}