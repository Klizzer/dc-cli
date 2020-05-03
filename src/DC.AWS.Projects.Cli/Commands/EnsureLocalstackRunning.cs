using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using CommandLine;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class EnsureLocalstackRunning
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        
        public static void Execute(Options options)
        {
            Console.WriteLine(options.GetRequiredServices().Any()
                ? $"Ensuring localstack is running with services: {string.Join(", ", options.GetRequiredServices())}"
                : "Ensuring localstack is running");

            var timeout = options.GetTimeout();

            var timer = Stopwatch.StartNew();

            while (timer.Elapsed < timeout)
            {
                try
                {
                    var response = HttpClient.GetAsync($"http://localhost:{options.Port}/health").Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = Json.DeSerialize<HealthResponse>(response.Content.ReadAsStringAsync().Result);

                        var requiredServices = options.GetRequiredServices().Any()
                            ? options.GetRequiredServices()
                            : responseData.Services.Keys.ToImmutableList();

                        if (responseData.Services.Any() 
                            && requiredServices.All(x => responseData.Services.ContainsKey(x) && responseData.Services[x] == "running"))
                        {
                            Console.WriteLine($"Running services: {string.Join(", ", responseData.Services.Keys)}");
                        
                            return;
                        }

                        Console.WriteLine("Localstack still not running...");
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Localstack still not running...");
                }
                
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
        
        [Verb("ensure-localstack", HelpText = "Ensure localstack is running.")]
        public class Options
        {
            [Option('p', "port", Default = "8055", HelpText = "Port where localstack is running.")]
            public string Port { get; set; }

            [Option('t', "timeout", Default = 60, HelpText = "Timeout in seconds.")]
            public int TimeoutSeconds { private get; set; }

            [Option('s', "services", Default = "", HelpText = "List of required services.")]
            public string RequiredServices { private get; set; }

            public TimeSpan GetTimeout()
            {
                return TimeSpan.FromSeconds(TimeoutSeconds);
            }

            public IImmutableList<string> GetRequiredServices()
            {
                return (RequiredServices ?? "")
                    .Split(',')
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToImmutableList();
            }
        }
        
        private class HealthResponse
        {
            public IImmutableDictionary<string, string> Services { get; set; }
        }
    }
}