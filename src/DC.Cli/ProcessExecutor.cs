using System.Diagnostics;

namespace DC.AWS.Projects.Cli
{
    public static class ProcessExecutor
    {
        public static (bool success, string output) Execute(string command, string arguments = "")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true
            };

            var process = Process.Start(startInfo);

            process?.WaitForExit();

            var output = process?.StandardOutput.ReadToEnd();

            return ((process?.ExitCode ?? 127) == 0, output);
        }
    }
}