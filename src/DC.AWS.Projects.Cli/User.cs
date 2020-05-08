using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DC.AWS.Projects.Cli
{
    public static class User
    {
        public static string GetHome()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Environment.GetEnvironmentVariable("HOME");
            
            var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
            if (string.IsNullOrWhiteSpace(homeDrive))
                throw new Exception("Environment variable error, there is no 'HOMEDRIVE'");

            var homePath = Environment.GetEnvironmentVariable("HOMEPATH");
            if (!string.IsNullOrWhiteSpace(homePath))
                return homeDrive + Path.DirectorySeparatorChar + homePath;

            throw new Exception("Environment variable error, there is no 'HOMEPATH'");
        }
    }
}