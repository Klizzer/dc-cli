using System;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Start
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = Components.Components.BuildTree(settings, options.Path);

            var restoreResult = await components.Start();
            
            Console.Write(restoreResult.Output);

            if (!restoreResult.Success)
                throw new StartFailedException(settings.GetRootedPath(options.Path));
        }
        
        [Verb("start", HelpText = "Start the application.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to start")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class StartFailedException : Exception
        {
            public StartFailedException(string path) : base($"Start failed at: \"{path}\"")
            {
                
            }
        }
    }
}