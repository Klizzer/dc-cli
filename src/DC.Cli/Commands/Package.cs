using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using ICSharpCode.SharpZipLib.Zip;

namespace DC.Cli.Commands
{
    public static class Package
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            var components = await Components.Components.BuildTree(settings, settings.GetRootedPath(options.Path));

            var results = await components.Package(options.GetVersion());

            var outputDirectory = new DirectoryInfo(settings.GetRootedPath(options.Output));
            
            if (!outputDirectory.Exists)
                outputDirectory.Create();
            
            foreach (var package in results)
            {
                await using var zipFile = File.Create(Path.Combine(outputDirectory.FullName, package.PackageName));
                await using var outStream = new ZipOutputStream(zipFile);
                
                foreach (var resource in package.Resources)
                {
                    outStream.PutNextEntry(new ZipEntry(resource.ResourceName));

                    await outStream.WriteAsync(resource.ResourceContent);
                        
                    outStream.CloseEntry();
                }

                await outStream.FlushAsync();
            }
        }
        
        [Verb("package", HelpText = "Package application")]
        public class Options
        {
            [Option('p', "path", HelpText = "Path to package.")]
            public string Path { get; set; } = Environment.CurrentDirectory;

            [Option('v', "package-version", HelpText = "Package version")]
            public string Version { private get; set; }

            [Option('o', "output", Default = "[[PROJECT_ROOT]]/.packages", HelpText = "Output directory.")]
            public string Output { get; set; }

            public string GetVersion()
            {
                var now = DateTime.UtcNow;

                var buildNumber = now.Hour * 60 * 60 + now.Minute * 60 + now.Second;

                return !string.IsNullOrEmpty(Version)
                    ? Version
                    : $"{now.Year}.{now.Month}.{now.Day}.{buildNumber}";
            }
        }
    }
}