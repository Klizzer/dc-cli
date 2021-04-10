using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace DC.Cli.Components
{
    public class PackageResult
    {
        public PackageResult(string packageName, Stream content)
        {
            PackageName = packageName;
            Content = content;

            Content.Position = 0;
        }

        public string PackageName { get; }
        public Stream Content { get; }

        public static async Task<PackageResult> FromResources(
            string packageName,
            IImmutableList<PackageResource> resources)
        {
            var output = new MemoryStream();
            var zipStream = new ZipOutputStream(output);
                
            foreach (var resource in resources)
            {
                zipStream.PutNextEntry(new ZipEntry(resource.ResourceName));

                await zipStream.WriteAsync(resource.ResourceContent);
                        
                zipStream.CloseEntry();
            }

            await zipStream.FlushAsync();

            await output.FlushAsync();
            output.Position = 0;

            return new PackageResult(packageName, output);
        }
    }
}