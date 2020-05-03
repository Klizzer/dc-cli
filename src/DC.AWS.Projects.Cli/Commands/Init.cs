using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Init
    {
        public static void Execute(Options options)
        {
            if (File.Exists(Path.Combine(options.GetRootedPath(), ".project.settings")))
                throw new InvalidOperationException("This project is already initialized.");
            
            var projectSettings = new ProjectSettings
            {
                DefaultLanguage = options.Language
            };

            var settings = Json.Serialize(projectSettings);
            
            File.WriteAllText(Path.Combine(options.GetRootedPath(), ".project.settings"), settings);
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            var projectFilesSourcePath = Path.Combine(executingAssembly.GetPath(), "Project");
            
            var projectDirectory = new DirectoryInfo(options.GetRootedPath());

            Directories.Copy(
                projectFilesSourcePath, 
                options.GetRootedPath(),
                ("PROJECT_NAME", projectDirectory.Name));

            Console.WriteLine(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $"Run command \"make -f Makefile.win init\" in \"./{projectDirectory.Name}\" to initialize the project."
                : $"Run command \"make init\" in \"./{projectDirectory.Name}\" to initialize the project.");
        }
        
        [Verb("init", HelpText = "Initialize a project here.")]
        public class Options
        {
            [Option('l', "lang", Default = SupportedLanguage.Node, HelpText = "Default language to use for functions")]
            public SupportedLanguage Language { get; set; }
            
            public string Path { private get; set; } = Environment.CurrentDirectory;

            public string GetRootedPath()
            {
                var path = Path;
                
                if (System.IO.Path.IsPathRooted(path))
                    return path;

                if (path.StartsWith("./"))
                    path = path.Substring(2);

                return System.IO.Path.Combine(Environment.CurrentDirectory, path);
            }
        }
    }
}