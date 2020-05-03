using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class SetupProxy
    {
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();
            
            var apiProxyConfig = new StringBuilder();

            foreach (var api in settings.Apis)
            {
                var proxyTemplateData = Assembly
                    .GetExecutingAssembly()
                    .GetManifestResourceStream("DC.AWS.Projects.Cli.Templates.Config.api-proxy.conf");

                var url = api.Value.BaseUrl;

                if (url.StartsWith("/"))
                    url = url.Substring(1);

                if (url.EndsWith("/"))
                    url = url.Substring(0, url.Length - 1);

                using (proxyTemplateData)
                using(var reader = new StreamReader(proxyTemplateData!))
                {
                    var templateData = reader.ReadToEnd();

                    templateData = templateData
                        .Replace("[[BASE_URL]]", url)
                        .Replace("[[SERVER_IP]]", GetLocalIpAddress())
                        .Replace("[[API_PORT]]", api.Value.Port.ToString());

                    apiProxyConfig.Append(templateData);
                }
            }
            
            File.WriteAllText(Path.Combine(settings.ProjectRoot, "config/.apis.conf"), apiProxyConfig.ToString());
        }

        private static string GetLocalIpAddress()
        {
            return (from item in NetworkInterface.GetAllNetworkInterfaces()
                    where item.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                          item.OperationalStatus == OperationalStatus.Up
                    from ip in item.GetIPProperties().UnicastAddresses
                    where ip.Address.AddressFamily == AddressFamily.InterNetwork
                    select ip.Address.ToString())
                .FirstOrDefault();
        }
        
        [Verb("setup-proxy", HelpText = "Setup proxy configuration.")]
        public class Options
        {
            
        }
    }
}