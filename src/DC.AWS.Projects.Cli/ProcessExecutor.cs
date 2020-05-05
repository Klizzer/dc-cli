using System.Diagnostics;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli
{
    public static class ProcessExecutor
    {
        public static async Task<Result> ExecuteBackground(string command, string arguments, string path)
        {
            return await await Task.Factory.StartNew(async () =>
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = path,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true
                });
            
                if (process == null)
                    return new Result(127, "");

                process.WaitForExit();

                var output = await process.StandardOutput.ReadToEndAsync();
            
                return new Result(process.ExitCode, output);
            });
        }
        
        public class Result
        {
            public Result(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }
            public string Output { get; }
        }
    }
}