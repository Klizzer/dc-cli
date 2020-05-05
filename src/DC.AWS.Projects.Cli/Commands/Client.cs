using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Client
    {
        private static readonly IImmutableDictionary<ClientType, Action<ProjectSettings, Options>> TypeHandlers =
            new Dictionary<ClientType, Action<ProjectSettings, Options>>
            {
                [ClientType.VueNuxt] = BuildVueNuxt
            }.ToImmutableDictionary();
        
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();
            
            Directory.CreateDirectory(options.GetRootedClientPath(settings));
            
            var url = options.BaseUrl;

            if (url.StartsWith("/"))
                url = url.Substring(1);

            if (url.EndsWith("/"))
                url = url.Substring(0, url.Length - 1);

            if (!string.IsNullOrEmpty(options.Api))
            {
                settings.AddClient(
                    options.Name,
                    options.BaseUrl,
                    options.GetRelativeClientPath(settings),
                    options.ClientType,
                    options.Api);

                if (!settings.Apis.ContainsKey(options.Api))
                {
                    Api.Execute(new Api.Options
                    {
                        Name = options.Api,
                        Path = options.Path,
                        BaseUrl = options.BaseUrl,
                        ExternalPort = options.ExternalPort
                    });

                    options.BaseUrl = "/";
                }
                
                Templates.Extract(
                    "client-proxy.conf",
                    Path.Combine(settings.ProjectRoot, settings.Apis[options.Api].RelativePath, $"_child_paths/{options.Name}.conf"),
                    Templates.TemplateType.Config,
                    ("BASE_URL", url),
                    ("UPSTREAM_NAME", $"{options.Name}-client-upstream"));
            }
            else
            {
                settings.AddClient(
                    options.Name,
                    options.BaseUrl,
                    options.GetRelativeClientPath(settings),
                    options.ClientType,
                    options.ExternalPort,
                    options.ExternalPort);
            }

            var clientServicePath = Path.Combine(settings.ProjectRoot, $"services/{options.Name}.client.make");

            if (!File.Exists(clientServicePath))
            {
                Templates.Extract(
                    "client.make",
                    clientServicePath,
                    Templates.TemplateType.Services,
                    ("CLIENT_NAME", options.Name),
                    ("PORT", settings.Clients[options.Name].Port.ToString()),
                    ("CLIENT_PATH", options.GetRelativeClientPath(settings)));
            }

            TypeHandlers[options.ClientType](settings, options);
            
            settings.Save();
        }

        private static void BuildVueNuxt(ProjectSettings settings, Options options)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "yarn",
                Arguments = $"create nuxt-app {options.Name}",
                WorkingDirectory = settings.GetRootedPath(options.Path)
            });

            process?.WaitForExit();
        }
        
        [Verb("client", HelpText = "Create a client application.")]
        public class Options
        {
            [Option('n', "name", Required = true, HelpText = "Name of the client.")]
            public string Name { get; set; }
            
            [Option('p', "path", Default = "[[PROJECT_ROOT]]/src", HelpText = "Path where to put the client.")]
            public string Path { get; set; }
            
            [Option('b', "base-url", Default = "/", HelpText = "Base url for the client.")]
            public string BaseUrl { get; set; }
    
            [Option('t', "type", Default = ClientType.VueNuxt, HelpText = "Client type.")]
            public ClientType ClientType { get; set; }
            
            [Option('o', "port", Default = 3000, HelpText = "Port to run client on.")]
            public int ExternalPort { get; set; }

            [Option('a', "api", HelpText = "Api to add client to.")]
            public string Api { get; set; }
            
            public string GetRelativeClientPath(ProjectSettings settings)
            {
                var dir = new DirectoryInfo(GetRootedClientPath(settings).Substring(settings.ProjectRoot.Length));

                return dir.FullName.Substring(1);
            }

            public string GetRootedClientPath(ProjectSettings settings)
            {
                return System.IO.Path.Combine(settings.GetRootedPath(Path), Name);
            }
        }
    }
}