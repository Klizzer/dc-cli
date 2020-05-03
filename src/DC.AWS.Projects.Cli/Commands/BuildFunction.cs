using System;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class BuildFunction
    {
        public static void Execute(Options options)
        {
            //TODO: Build function
        }
        
        [Verb("build-func", HelpText = "Build a function.")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to the function.")]
            public string Path { get; set; } = Environment.CurrentDirectory;
        }
    }
}