using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli
{
    public static class Docker
    {
        public static Container TemporaryContainerFromImage(string image)
        {
            return ContainerFromImage(image, null);
        }
        
        public static Container ContainerFromImage(string image, string name)
        {
            Pull(image);

            var container = new Container(name, image);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                container = container.WithArgument("--user \"$(id -u):$(id -g)\"");

            return container;
        }
        
        public static Container ContainerFromFile(string path, string imageName, string containerName)
        {
            var containerPath = path;

            if (!Path.IsPathRooted(path))
            {
                containerPath = Path.Combine(
                    Assembly.GetExecutingAssembly().GetPath(),
                    $"Containers/{path}");
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"build -t \"{imageName}\" \"{containerPath}\""
            };
            
            var process = Process.Start(startInfo);

            process?.WaitForExit();
            
            return ContainerFromImage(imageName, containerName);
        }

        public static void Pull(string image)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"pull {image}"
            };
            
            var process = Process.Start(startInfo);

            process?.WaitForExit();
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
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? WithVolume("/usr/local/bin/docker", "/usr/bin/docker") 
                    : WithVolume("/var/run/docker.sock", "/var/run/docker.sock");
            }

            public Container WithVolume(string source, string destination, bool useAsWorkDir = false)
            {
                var container = WithArgument($"-v \"{source}:{destination}\"");
                
                return useAsWorkDir ? container.WorkDir(destination) : container;
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
                return new Container(null, _image, _interactive, _dockerArguments);
            }
            
            public Container WithArgument(string argument)
            {
                var newArguments = !_dockerArguments.Contains(argument) 
                    ? _dockerArguments.Add(argument)
                    : _dockerArguments;
                
                return new Container(Name, _image, _interactive, newArguments);
            }

            public async Task<(int exitCode, string output)> Run(string command)
            {
                var arguments = string.Join(" ", _dockerArguments);

                if (!string.IsNullOrEmpty(Name))
                    arguments = $"--name {Name} {arguments}";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"run {arguments} {_image} {command}"
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