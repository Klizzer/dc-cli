using System;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Build
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, options.Path);
            
            var result = await components.Build();
            
            Console.Write(result.Output);

            if (!result.Success)
                throw new BuildFailedException(settings.GetRootedPath(options.Path));
        }
        
        [Verb("build", HelpText = "Build the project.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to build")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
        
        private class BuildFailedException : Exception
        {
            public BuildFailedException(string path) : base($"Build failed at: \"{path}\"")
            {
                
            }
        }
    }
}