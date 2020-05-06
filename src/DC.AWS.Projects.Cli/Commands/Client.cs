using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Components;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class Client
    {
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();

            await ClientComponent.InitAt(
                settings,
                options.GetRootedClientPath(settings),
                options.BaseUrl,
                options.ClientType,
                !string.IsNullOrEmpty(options.Api) ? (int?) null : options.ExternalPort);

            if (!string.IsNullOrEmpty(options.Api))
            {
                if (!settings.Apis.ContainsKey(options.Api))
                    throw new InvalidOperationException($"There is no api named: {options.Api}");

                await LocalProxyComponent.AddChildTo(
                    settings,
                    settings.GetRootedPath(settings.Apis[options.Api].RelativePath),
                    options.BaseUrl,
                    $"{options.Name}-client-upstream");
            }
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