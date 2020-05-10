using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DC.Cli.Components;

namespace DC.Cli
{
    public class ProjectSettings
    {
        private const string ProjectNameKey = "projectName";

        private readonly IDictionary<string, string> _projectSettings;
        private readonly IDictionary<string, string> _userSettings;
        
        private ProjectSettings(
            IDictionary<string, string> projectSettings,
            IDictionary<string, string> userSettings,
            string projectRoot)
        {
            ProjectRoot = projectRoot;
            _projectSettings = projectSettings;
            _userSettings = userSettings;
        }

        public string ProjectRoot { get; }

        public string GetProjectName()
        {
            return GetConfiguration(ProjectNameKey);
        }
        
        public static ProjectSettings New(string path, string name)
        {
            var projectSettings = new Dictionary<string, string>
            {
                [ProjectNameKey] = name
            };
            
            return new ProjectSettings(
                projectSettings,
                new Dictionary<string, string>(), 
                path);
        }
        
        public static async Task<ProjectSettings> Read()
        {
            var currentPath = Environment.CurrentDirectory;
            
            while (true)
            {
                if (File.Exists(Path.Combine(currentPath, ".project.settings")))
                {
                    var projectSettings = Json.DeSerialize<IDictionary<string, string>>(
                        await File.ReadAllTextAsync(Path.Combine(currentPath, ".project.settings")));
                    
                    var userSettings = new Dictionary<string, string>();

                    if (File.Exists(Path.Combine(currentPath, ".user.settings")))
                    {
                        userSettings = Json.DeSerialize<Dictionary<string, string>>(
                            await File.ReadAllTextAsync(Path.Combine(currentPath, ".user.settings")));
                    }

                    return new ProjectSettings(projectSettings, userSettings, currentPath);
                }

                currentPath = Directory.GetParent(currentPath).FullName;
            }
        }

        public async Task Save()
        {
            await File.WriteAllTextAsync(Path.Combine(ProjectRoot, ".project.settings"), Json.Serialize(_projectSettings));

            if (_userSettings.Any())
            {
                await File.WriteAllTextAsync(Path.Combine(ProjectRoot, ".user.settings"),
                    Json.Serialize(_userSettings));
            }
        }

        public string GetConfiguration(string key, string defaultValue = "")
        {
            if (_userSettings.ContainsKey(key))
                return _userSettings[key];

            return _projectSettings.ContainsKey(key) ? _projectSettings[key] : defaultValue;
        }

        public IImmutableDictionary<string, string> GetAllConfigurations(string configNamespace, string defaultValue = "")
        {
            var keys = _userSettings
                .Keys
                .Where(x => x.StartsWith(configNamespace))
                .Select(x => x.Substring(configNamespace.Length))
                .Union(_projectSettings
                    .Keys
                    .Where(x => x.StartsWith(configNamespace))
                    .Select(x => x.Substring(configNamespace.Length)));

            return keys.ToImmutableDictionary(x => x, x => GetConfiguration(x, defaultValue));
        }

        public bool HasConfiguration(string key)
        {
            return _userSettings.ContainsKey(key) || _projectSettings.ContainsKey(key);
        }

        public void SetConfiguration(string key, string value, INeedConfiguration.ConfigurationType configurationType)
        {
            switch (configurationType)
            {
                case INeedConfiguration.ConfigurationType.Project:
                    _projectSettings[key] = value;
                    
                    break;
                case INeedConfiguration.ConfigurationType.User:
                    _userSettings[key] = value;
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(configurationType), configurationType, null);
            }
        }
        
        public string GetRootedPath(string path)
        {
            path = path.Replace("[[PROJECT_ROOT]]", ProjectRoot);
            
            if (Path.IsPathRooted(path))
                return path;

            if (path.StartsWith("./"))
                path = path.Substring(2);

            return Path.Combine(ProjectRoot, path);
        }

        public string GetRelativePath(string path, string fromPath = null)
        {
            return GetRootedPath(path).Substring(GetRootedPath(fromPath ?? "").Length).Substring(1);
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