using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DC.AWS.Projects.Cli
{
    public class ProjectSettings
    {
        private ProjectSettings()
        {
            
        }
        
        public string DefaultLanguage { private get; set; }

        [JsonIgnore]
        public string ProjectRoot { get; set; }

        public ILanguageVersion GetDefaultLanguage()
        {
            return FunctionLanguage.Parse(DefaultLanguage);
        }

        public static ProjectSettings New(ILanguageVersion defaultLanguage, string path)
        {
            return new ProjectSettings
            {
                DefaultLanguage = defaultLanguage?.ToString(),
                ProjectRoot = path
            };
        }
        
        public static async Task<ProjectSettings> Read()
        {
            var currentPath = Environment.CurrentDirectory;
            
            while (true)
            {
                if (File.Exists(Path.Combine(currentPath, ".project.settings")))
                {
                    var settings = Json.DeSerialize<ProjectSettings>(
                        await File.ReadAllTextAsync(Path.Combine(currentPath, ".project.settings")));

                    settings.ProjectRoot = currentPath;

                    return settings;
                }

                currentPath = Directory.GetParent(currentPath).FullName;
            }
        }

        public Task Save()
        {
            return File.WriteAllTextAsync(Path.Combine(ProjectRoot, ".project.settings"), Json.Serialize(this));
        }
        
        public string GetRootedPath(string path)
        {
            path = path.Replace("[[PROJECT_ROOT]]", ProjectRoot);
            
            if (Path.IsPathRooted(path))
                return path;

            if (path.StartsWith("./"))
                path = path.Substring(2);

            return Path.Combine(Environment.CurrentDirectory, path);
        }

        public string GetRelativePath(string path)
        {
            return GetRootedPath(path).Substring(ProjectRoot.Length).Substring(1);
        }
        
        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}