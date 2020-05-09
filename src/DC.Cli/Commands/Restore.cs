using System;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Restore
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.Path);

            var restoreResult = await components.Restore();
            
            Console.Write(restoreResult.Output);

            if (!restoreResult.Success)
                throw new RestoreFailedException(settings.GetRootedPath(options.Path));
        }
        
        [Verb("restore")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to restore")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class RestoreFailedException : Exception
        {
            public RestoreFailedException(string path) : base($"Restore failed at: \"{path}\"")
            {
                
            }
        }
    }
}