using System.Collections.Immutable;
using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IPackageApplication : IComponent
    {
        Task<IImmutableList<PackageResult>> Package(IImmutableList<PackageResource> resources, string version);
    }
}