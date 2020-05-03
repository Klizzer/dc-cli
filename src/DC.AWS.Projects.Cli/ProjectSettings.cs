using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DC.AWS.Projects.Cli
{
    public class ProjectSettings
    {
        public SupportedLanguage DefaultLanguage { get; set; }
        public IDictionary<string, ApiConfiguration> Apis { get; set; } = new Dictionary<string, ApiConfiguration>();
        
        [JsonIgnore]
        public string ProjectRoot { get; set; }

        public void AddApi(string name, string baseUrl, SupportedLanguage? defaultLanguage, int port)
        {
            Apis[name] = new ApiConfiguration
            {
                BaseUrl = baseUrl,
                DefaultLanguage = defaultLanguage,
                Port = port
            };
        }

        public SupportedLanguage GetLanguage(string api = null)
        {
            return Apis.ContainsKey(api ?? "") ? Apis[api ?? ""].DefaultLanguage ?? DefaultLanguage : DefaultLanguage;
        }

        public static ProjectSettings Read()
        {
            var currentPath = Environment.CurrentDirectory;
            
            while (true)
            {
                if (File.Exists(Path.Combine(currentPath, ".project.settings")))
                {
                    var settings = Json.DeSerialize<ProjectSettings>(
                        File.ReadAllText(Path.Combine(currentPath, ".project.settings")));

                    settings.ProjectRoot = currentPath;

                    return settings;
                }

                currentPath = Directory.GetParent(currentPath).FullName;
            }
        }
        
        public (string path, string name) FindFunctionRoot(string path)
        {
            return FindRoot(path, "function");
        }

        public (string path, string name) FindApiRoot(string path)
        {
            return FindRoot(path, "api");
        }

        public string FindApiPath(string name)
        {
            var path = FindPath("api", name, ProjectRoot);
            
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException($"There is no api named: {name}");

            return path;
        }

        public bool HasApiAt(string path)
        {
            var currentPath = path;

            while (true)
            {
                if (!currentPath.StartsWith(ProjectRoot))
                    return false;
                
                if (File.Exists(Path.Combine(currentPath, $"api.tf")))
                    return true;
                
                currentPath = Directory.GetParent(currentPath).FullName;
            }
        }

        public string GetApiFunctionUrl(string api, string functionUrl)
        {
            if (!Apis.ContainsKey(api))
                throw new InvalidOperationException($"There is no api named: {api}");

            var apiBaseUrl = Apis[api].BaseUrl;

            if (apiBaseUrl.StartsWith("/"))
                apiBaseUrl = apiBaseUrl.Substring(1);
            
            if (apiBaseUrl.EndsWith("/"))
                apiBaseUrl = apiBaseUrl.Substring(0, apiBaseUrl.Length - 1);

            if (functionUrl.StartsWith("/"))
                functionUrl = functionUrl.Substring(1);

            return string.IsNullOrEmpty(apiBaseUrl) ? $"/{functionUrl}" : $"/{apiBaseUrl}/{functionUrl}";
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
        
        private (string path, string name) FindRoot(string path, string type)
        {
            var currentPath = path;

            while (true)
            {
                if (!currentPath.StartsWith(ProjectRoot))
                    throw new InvalidOperationException("Can't search paths outside of project directory");
                
                if (File.Exists(Path.Combine(currentPath, $"{type}.infra.yml")))
                    return (currentPath, new DirectoryInfo(currentPath).Name);
                
                currentPath = Directory.GetParent(currentPath).FullName;
            }
        }

        private static string FindPath(string type, string name, string currentPath)
        {
            var directoriesToSkip = new List<string>
            {
                "node_modules",
                "infrastructure"
            };
            
            var dir = new DirectoryInfo(currentPath);

            if (dir.Name == name && File.Exists(Path.Combine(dir.FullName, $"{type}.infra.yml")))
                return dir.FullName;
            
            var dirs = dir.GetDirectories();

            return (from child in dirs
                    where !directoriesToSkip.Contains(child.Name)
                    select FindPath(type, name, child.FullName))
                .FirstOrDefault(foundPath => !string.IsNullOrEmpty(foundPath));
        }
        
        public class ApiConfiguration
        {
            public string BaseUrl { get; set; }
            public SupportedLanguage? DefaultLanguage { get; set; }
            public int Port { get; set; }
        }
    }
}