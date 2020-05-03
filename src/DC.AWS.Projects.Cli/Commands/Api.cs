using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Api
    {
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();
            
            if (settings.Apis.ContainsKey(options.Name))
                throw new InvalidOperationException($"There is already a api named: \"{options.Name}\"");
            
            if (Directory.Exists(options.GetRootedApiPath(settings)))
                throw new InvalidOperationException($"You can't add a new api at: \"{options.GetRootedApiPath(settings)}\". It already exists.");

            Directory.CreateDirectory(options.GetRootedApiPath(settings));

            InfrastructureTemplates.Extract(
                "api.infra.yml",
                Path.Combine(options.GetRootedApiPath(settings), "api.infra.yml"),
                ("API_NAME", options.Name));

            settings.AddApi(options.Name, options.BaseUrl, options.DefaultLanguage, GetRandomUnusedPort());
            
            File.WriteAllText(Path.Combine(settings.ProjectRoot, ".project.settings"), Json.Serialize(settings));
        }
        
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Verb("api", HelpText = "Create a api.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the api.")]
            public string Name { get; set; }

            [Option('b', "base-url", Default = "/", HelpText = "Base url for the api.")]
            public string BaseUrl { get; set; }
            
            [Option('p', "path", Default = "[[PROJECT_ROOT]]/src", HelpText = "Path where to put the api.")]
            public string Path { private get; set; }

            [Option('l', "lang", HelpText = "Default language for api functions.")]
            public SupportedLanguage? DefaultLanguage { get; set; }
            
            public string GetRootedBasePath(ProjectSettings projectSettings)
            {
                return projectSettings.GetRootedPath(Path);
            }
            
            public string GetRootedApiPath(ProjectSettings projectSettings)
            {
                return System.IO.Path.Combine(GetRootedBasePath(projectSettings), Name);
            }
        }
    }
}