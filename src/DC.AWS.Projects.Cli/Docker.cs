using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli
{
    public static class Docker
    {
        public static Container TemporaryContainerFromImage(string image)
        {
            var initialArguments = new List<string>();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                initialArguments.Add("--user \"$(id -u):$(id -g)\"");
            
            return new Container(Guid.NewGuid().ToString(), image, initialArguments.ToImmutableList());
        }
        
        public static Container ContainerFromImage(string image, string name)
        {
            var initialArguments = new List<string>();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                initialArguments.Add("--user \"$(id -u):$(id -g)\"");
            
            return new Container(name, image, initialArguments.ToImmutableList());
        }
        
        public static Container ContainerFromFile(string path, string imageName, string containerName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"build -t \"{imageName}\" \"{path}\""
            };
            
            var process = Process.Start(startInfo);

            process?.WaitForExit();
            
            return ContainerFromImage(imageName, containerName);
        }

        public static void Stop(string name)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"stop {name}"
            };
 
            var process = Process.Start(startInfo);

            process?.WaitForExit();
        }

        public static void Remove(string name)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"stop container rm {name}"
            };
 
            var process = Process.Start(startInfo);

            process?.WaitForExit();
        }

        public static Task<string> Logs(string name)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"logs {name}",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };


            var process = Process.Start(startInfo);

            process?.WaitForExit();

            return process == null ? Task.FromResult("") : process.StandardOutput.ReadToEndAsync();
        }
        
        public class Container
        {
            private readonly bool _interactive;
            private readonly string _image;
            private readonly IImmutableList<string> _dockerArguments;

            public Container(string name, string image, IImmutableList<string> initialArguments = null) 
                : this(name, image, false, initialArguments ?? new List<string>().ToImmutableList())
            {
            }

            private Container(string name, string image, bool interactive, IImmutableList<string> dockerArguments)
            {
                _image = image;
                _interactive = interactive;
                _dockerArguments = dockerArguments;
                Name = name;
            }

            public string Name { get; }

            public Container Interactive()
            {
                return WithArgument("-it");
            }

            public Container Detached()
            {
                return WithArgument("-d");
            }

            public Container Port(int host, int container)
            {
                return WithArgument($"-p {host}:{container}");
            }

            public Container EnvironmentVariable(string name, string value)
            {
                return WithArgument($"-e {name}=\"{value}\"");
            }

            public Container WithDockerSocket()
            {
                return WithVolume("/var/run/docker.sock", "/var/run/docker.sock");
            }

            public Container WithVolume(string source, string destination, bool useAsWorkDir = false)
            {
                var container = WithArgument($"-v \"{source}:{destination}\"");
                
                return useAsWorkDir ? container.EntryPoint(destination) : container;
            }

            public Container WorkDir(string path)
            {
                return WithArgument($"--workdir \"{path}\"");
            }

            public Container EntryPoint(string command)
            {
                return WithArgument($"--entrypoint {command}");
            }

            public Container Temporary()
            {
                return new Container(Guid.NewGuid().ToString(), _image, _interactive, _dockerArguments);
            }
            
            private Container WithArgument(string argument)
            {
                var newArguments = !_dockerArguments.Contains(argument) 
                    ? _dockerArguments.Add(argument)
                    : _dockerArguments;
                
                return new Container(Name, _image, _interactive, newArguments);
            }

            public async Task<(int exitCode, string output)> Run(string command)
            {
                var arguments = string.Join(" ", _dockerArguments);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"run --name {Name} {arguments} {_image} {command}"
                };

                Func<Process, Task<string>> getOutput = x => Task.FromResult("");

                if (!_interactive)
                {
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.CreateNoWindow = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardInput = true;

                    getOutput = x => x.StandardOutput.ReadToEndAsync();
                }
                
                var process = Process.Start(startInfo);

                process?.WaitForExit();

                var output = await getOutput(process);

                return (process?.ExitCode ?? 127, output);
            }
        }
    }
}