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
                options.Port);
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
            
            [Option('o', "port", HelpText = "Port to run client on.")]
            public int? Port { get; set; }
            
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