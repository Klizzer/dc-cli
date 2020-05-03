using System.IO;

namespace DC.AWS.Projects.Cli
{
    public class ProjectConfiguration
    {
        public string LocalstackApiKey { get; set; }
        
        public static ProjectConfiguration Read(string path)
        {
            var currentPath = path;
            
            while (true)
            {
                if (File.Exists(Path.Combine(currentPath, ".settings.json")))
                {
                    return Json.DeSerialize<ProjectConfiguration>(
                        File.ReadAllText(Path.Combine(currentPath, ".settings.json")));
                }

                currentPath = Directory.GetParent(currentPath).FullName;
            }
        }
    }
}