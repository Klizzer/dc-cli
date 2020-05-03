using System.IO;
using System.Linq;

namespace DC.AWS.Projects.Cli
{
    public static class Directories
    {
        public static void Copy(
            string sourceDirName,
            string destDirName,
            params (string name, string value)[] variables)
        {
            var dir = new DirectoryInfo(sourceDirName);
            var dirs = dir.GetDirectories();

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");

            if (!Directory.Exists(destDirName))
                Directory.CreateDirectory(destDirName);

            var files = dir.GetFiles();
            foreach (var file in files)
            {
                var fileContent = File.ReadAllText(file.FullName);

                fileContent = variables
                    .Aggregate(
                        fileContent,
                        (current, variable) => current
                            .Replace($"[[{variable.name}]]", variable.value));

                var fileDestination = Path.Combine(destDirName, file.Name);
                
                File.WriteAllText(fileDestination, fileContent);
            }

            foreach (var subdir in dirs)
            {
                var tempPath = Path.Combine(destDirName, subdir.Name);
                
                Copy(subdir.FullName, tempPath, variables);
            }
        }
    }
}