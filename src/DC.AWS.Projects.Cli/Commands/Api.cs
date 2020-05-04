using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
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
            
            settings.AddApi(
                options.Name,
                options.BaseUrl,
                options.GetRelativeApiPath(settings),
                options.GetLanguage(), 
                options.ExternalPort);

            Templates.Extract(
                "api.infra.yml",
                Path.Combine(options.GetRootedApiPath(settings), "api.infra.yml"),
                Templates.TemplateType.Infrastructure,
                ("API_NAME", options.Name));
            
            var url = options.BaseUrl;

            if (url.StartsWith("/"))
                url = url.Substring(1);

            if (url.EndsWith("/"))
                url = url.Substring(0, url.Length - 1);
            
            Templates.Extract(
                "proxy.conf",
                Path.Combine(options.GetRootedApiPath(settings), "proxy.nginx.conf"),
                Templates.TemplateType.Config,
                ("BASE_URL", url),
                ("SERVER_IP", GetLocalIpAddress()),
                ("API_PORT", settings.Apis[options.Name].Port.ToString()));
            
            settings.Save();
        }
        
        public static string GetLocalIpAddress()
        {
            return (from item in NetworkInterface.GetAllNetworkInterfaces()
                    where item.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                          item.OperationalStatus == OperationalStatus.Up
                    from ip in item.GetIPProperties().UnicastAddresses
                    where ip.Address.AddressFamily == AddressFamily.InterNetwork
                    select ip.Address.ToString())
                .FirstOrDefault();
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
            public string DefaultLanguage { private get; set; }

            [Option('o', "port", Default = 4000, HelpText = "Port to run api on.")]
            public int ExternalPort { get; set; }
            
            public string GetRootedApiPath(ProjectSettings projectSettings)
            {
                return System.IO.Path.Combine(projectSettings.GetRootedPath(Path), Name);
            }
            
            public string GetRelativeApiPath(ProjectSettings settings)
            {
                var dir = new DirectoryInfo(GetRootedApiPath(settings).Substring(settings.ProjectRoot.Length));

                return dir.FullName.Substring(1);
            }

            public ILanguageRuntime GetLanguage()
            {
                return string.IsNullOrEmpty(DefaultLanguage) ? null : FunctionLanguage.Parse(DefaultLanguage);
            }
        }
    }
}