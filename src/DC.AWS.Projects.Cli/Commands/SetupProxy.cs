using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class SetupProxy
    {
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();
            
            var destination = Path.Combine(settings.ProjectRoot, "config/.generated");
            
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);

            Directory.CreateDirectory(destination);
            
            foreach (var api in settings.Apis)
            {
                File.WriteAllText(Path.Combine(destination, $"api-{api.Key}.proxy"), Json.Serialize(new
                {
                    Port = api.Value.ExternalPort,
                    Path = api.Value.RelativePath
                }));
            }
            
            foreach (var client in settings.Clients)
            {
                if (string.IsNullOrEmpty(client.Value.Api))
                {
                    File.WriteAllText(Path.Combine(destination, $"client-{client.Key}.proxy"), Json.Serialize(new
                    {
                        Port = client.Value.ExternalPort,
                        Path = client.Value.RelativePath
                    }));
                }
            }
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