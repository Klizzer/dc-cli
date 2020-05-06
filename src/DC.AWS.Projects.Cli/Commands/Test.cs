using System;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Test
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = Components.Components.BuildTree(settings, options.Path);

            var testResult = await components.Test();
            
            Console.Write(testResult.Output);

            if (!testResult.Success)
                throw new TestsFailedException(settings.GetRootedPath(options.Path));
        }
        
        [Verb("test", HelpText = "Run all tests.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to test")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class TestsFailedException : Exception
        {
            public TestsFailedException(string path) : base($"Tests failed at: \"{path}\"")
            {
                
            }
        }
    }
}