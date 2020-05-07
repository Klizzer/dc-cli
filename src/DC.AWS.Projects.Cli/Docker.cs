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
        public static Container CreateContainer(string image)
        {
            var initialArguments = new List<string>();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                initialArguments.Add("--user \"$(id -u):$(id -g)\"");
            
            return new Container(image, initialArguments.ToImmutableList());
        }
        
        public class Container
        {
            private readonly bool _interactive;
            private readonly string _image;
            private readonly IImmutableList<string> _dockerArguments;

            public Container(string image, IImmutableList<string> initialArguments = null) 
                : this(image, false, initialArguments ?? new List<string>().ToImmutableList())
            {
            }

            private Container(string image, bool interactive, IImmutableList<string> dockerArguments)
            {
                _image = image;
                _interactive = interactive;
                _dockerArguments = dockerArguments;
            }

            public Container Interactive()
            {
                var newArguments = !_dockerArguments.Contains("-it") 
                    ? _dockerArguments.Add("-it")
                    : _dockerArguments;
                
                return new Container(_image, true, newArguments);
            }

            public Container WithVolume(string source, string destination, bool useAsWorkDir = false)
            {
                var newArguments = _dockerArguments.Add($"-v \"{source}:{destination}\"");
                
                var container = new Container(_image, _interactive, newArguments);

                return useAsWorkDir ? container.EntryPoint(destination) : container;
            }

            public Container WorkDir(string path)
            {
                var newArguments = _dockerArguments.Add($"--workdir \"{path}\"");

                return new Container(_image, _interactive, newArguments);
            }

            public Container EntryPoint(string command)
            {
                var newArguments = _dockerArguments.Add($"--entrypoint {command}");

                return new Container(_image, _interactive, newArguments);
            }

            public async Task<(int exitCode, string output)> Run(string command)
            {
                var arguments = string.Join(" ", _dockerArguments);
                
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