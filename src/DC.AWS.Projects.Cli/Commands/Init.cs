using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Init
    {
        public static async Task Execute(Options options)
        {
            if (File.Exists(Path.Combine(options.GetRootedPath(), ".project.settings")))
                throw new InvalidOperationException("This project is already initialized.");

            var projectDirectory = new DirectoryInfo(options.GetRootedPath());
            
            var projectSettings = ProjectSettings.New(
                options.GetRootedPath(),
                projectDirectory.Name);

            await projectSettings.Save();
            
            var executingAssembly = Assembly.GetExecutingAssembly();

            var projectFilesSourcePath = Path.Combine(executingAssembly.GetPath(), "Project");

            await Directories.Copy(
                projectFilesSourcePath, 
                options.GetRootedPath(),
                ("PROJECT_NAME", projectSettings.GetProjectName()),
                ("NUGET_FEED_URL", options.NugetFeed),
                ("DC_CLI_VERSION", GetVersion()));
            
            var makeInitProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "make",
                Arguments = "init",
                WorkingDirectory = projectDirectory.FullName
            });

            makeInitProcess?.WaitForExit();

            var gitInitProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init",
                WorkingDirectory = projectDirectory.FullName
            });

            gitInitProcess?.WaitForExit();
        }

        private static string GetVersion()
        {
            var assembly = Assembly
                .GetExecutingAssembly();
            
            return assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "";
        }
        
        [Verb("init", HelpText = "Initialize a project here.")]
        public class Options
        {

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
        }
    }
}