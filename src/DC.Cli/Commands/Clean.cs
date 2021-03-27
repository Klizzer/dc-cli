using System;
using System.Threading.Tasks;
using CommandLine;

namespace DC.Cli.Commands
{
    public static class Clean
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.Path);

            var cleanResult = await components.Clean();
            
            if (!cleanResult)
                throw new CleanFailedException(settings.GetRootedPath(options.Path));
        }
        
        [Verb("clean")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to clean")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class CleanFailedException : Exception
        {
            public CleanFailedException(string path) : base($"Clean failed at: \"{path}\"")
            {
                
            }
        }
    }
}