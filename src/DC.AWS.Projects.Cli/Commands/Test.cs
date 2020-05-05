using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Test
    {
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();

            var failures = TestPath(settings.GetRootedPath(options.Path));

            if (failures.Any())
                throw new TestsFailedException(failures.SelectMany(x => x.FailureLocations).Distinct().ToArray());
        }

        private static IImmutableList<TestsFailedException> TestPath(string path)
        {
            var dir = new DirectoryInfo(path);
            
            var failures = new List<TestsFailedException>();

            if (File.Exists(Path.Combine(dir.FullName, "function.infra.yml")))
            {
                try
                {
                    TestFunction.Execute(new TestFunction.Options
                    {
                        Path = path
                    });
                }
                catch (TestsFailedException exception)
                {
                    failures.Add(exception);
                }
            }

            foreach (var subDir in dir.GetDirectories())
            {
                if (subDir.Name == "node_modules")
                    continue;
                
                failures.AddRange(TestPath(subDir.FullName));
            }

            return failures.ToImmutableList();
        }
        
        [Verb("test", HelpText = "Run all tests.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to test")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
    }
}