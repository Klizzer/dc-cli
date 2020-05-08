using System;
using System.IO;
using System.Reflection;

namespace DC.AWS.Projects.Cli
{
    public static class AssemblyExtensions
    {
        public static string GetPath(this Assembly assembly)
        {
            var codeBase = assembly.CodeBase;
            
            var uri = new UriBuilder(codeBase ?? throw new ArgumentNullException(nameof(codeBase)));
            
            var path = Uri.UnescapeDataString(uri.Path);
            
            return Path.GetDirectoryName(path);
        }

        public static string GetInformationVersion(this Assembly assembly)
        {
            return assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "";
        }
    }
}