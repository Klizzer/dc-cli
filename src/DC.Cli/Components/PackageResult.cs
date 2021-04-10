using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace DC.Cli.Components
{
    public class PackageResult
    {
        public PackageResult(string packageName, byte[] content)
        {
            PackageName = packageName;
            Content = content;
        }

        public string PackageName { get; }
        public byte[] Content { get; }

        public static async Task<PackageResult> FromResources(
            string packageName,
            IImmutableList<PackageResource> resources)
        {
            await using var output = new MemoryStream();
            await using var zipStream = new ZipOutputStream(output);

            foreach (var resource in resources)
            {
                zipStream.PutNextEntry(new ZipEntry(resource.ResourceName));

                await zipStream.WriteAsync(resource.ResourceContent);
                        
                zipStream.CloseEntry();
            }

            zipStream.Finish();

            output.Position = 0;

            return new PackageResult(packageName, output.ToArray());
        }
    }
}