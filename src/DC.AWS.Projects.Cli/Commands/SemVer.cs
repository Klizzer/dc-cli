using System;
using System.Reflection;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class SemVer
    {
        public static void Execute(Options options)
        {
            Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString());
        }
        
        [Verb("semver", HelpText = "Gets the version")]
        public class Options
        {
            
        }
    }
}