using System.Collections.Immutable;
using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IHavePackageResources : IComponent
    {
        Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components,
            string version);
    }
}