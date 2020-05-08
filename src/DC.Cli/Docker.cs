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
            return CreateContainer(image, name);
        }
        
        public static Container ContainerFromProject(string name, string imageName, string containerName)
        {
            var imageTag = $"{imageName}:{Assembly.GetExecutingAssembly().GetInformationVersion()}";

            if (HasImage(imageTag)) 
                return CreateContainer(imageName, containerName);

            var fileData = GetProjectDockerContent(name);

            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"build -t {imageTag} -",
                RedirectStandardInput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = Process.Start(startInfo);

            process?.StandardInput.Write(fileData);
            process?.StandardInput.Flush();
            process?.StandardInput.Close();

            process?.WaitForExit();
            process?.CloseMainWindow();
            process?.Close();

            return CreateContainer(imageName, containerName);
        }

        private static Container CreateContainer(string image, string name)
        {
            return new Container(name, image);
        }
        
        private static string GetProjectDockerContent(string name)
        {
            var dockerFileData = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"DC.Cli.Containers.{name}.Dockerfile");

            using (dockerFileData)
            using(var reader = new StreamReader(dockerFileData!))
            {
                return reader.ReadToEnd();
            }
        }

        public static bool HasImage(string image)
        {
            var result = ProcessExecutor.Execute("docker", $"images -q {image}");

            return result.success && !string.IsNullOrEmpty(result.output);
        }
        
        public static void Pull(string image)
        {
            ProcessExecutor.Execute("docker", $"pull {image}");
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
                Arguments = $"container rm {name}"
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
                return new Container(Name, _image, true, _dockerArguments)
                    .WithArgument("-it");
            }

            public Container AsCurrentUser()
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return this;

                var userId = ProcessExecutor.Execute("id", "-u").output.Split('\n').FirstOrDefault();
                var groupId = ProcessExecutor.Execute("id", "-g").output.Split('\n').FirstOrDefault();
                
                return WithArgument($"--user \"{userId}:{groupId}\"");
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