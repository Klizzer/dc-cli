using System;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Stop
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.Path);

            var restoreResult = await components.Stop();
            
            Console.Write(restoreResult.Output);

            if (!restoreResult.Success)
                throw new StopFailedException(settings.GetRootedPath(options.Path));
        }
        
        [Verb("stop", HelpText = "Stop the application.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to stop")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class StopFailedException : Exception
        {
            public StopFailedException(string path) : base($"Stop failed at: \"{path}\"")
            {
                
            }
        }
    }
}