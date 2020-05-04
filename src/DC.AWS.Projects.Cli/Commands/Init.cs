using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Init
    {
        public static void Execute(Options options)
        {
            if (File.Exists(Path.Combine(options.GetRootedPath(), ".project.settings")))
                throw new InvalidOperationException("This project is already initialized.");

            var projectSettings = ProjectSettings.New(options.GetLanguage(), options.GetRootedPath());

            projectSettings.Save();
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            var projectFilesSourcePath = Path.Combine(executingAssembly.GetPath(), "Project");
            
            var projectDirectory = new DirectoryInfo(options.GetRootedPath());

            Directories.Copy(
                projectFilesSourcePath, 
                options.GetRootedPath(),
                ("PROJECT_NAME", projectDirectory.Name),
                ("AWS_REGION", options.AwsRegion),
                ("NUGET_FEED_URL", options.NugetFeed));
            
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "make",
                Arguments = "init",
                WorkingDirectory = projectDirectory.FullName
            });

            process?.WaitForExit();
        }
        
        [Verb("init", HelpText = "Initialize a project here.")]
        public class Options
        {
            [Option('l', "lang", Default = FunctionLanguage.DefaultLanguage, HelpText = "Default language to use for functions")]
            public string Language { private get; set; }
            
            [Option('r', "aws-region", Default = "eu-north-1", HelpText = "Aws region.")]
            public string AwsRegion { get; set; }
            
            [Option('f', "nuget-feed", Default = "", HelpText = "Nuget feed to publish packages to.")]
            public string NugetFeed { get; set; }
            
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

            public ILanguageRuntime GetLanguage()
            {
                return FunctionLanguage.Parse(Language);
            }
        }
    }
}