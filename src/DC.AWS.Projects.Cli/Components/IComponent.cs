using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponent
    {
        string Name { get; }
    }

    public interface IStartableComponent : IComponent
    {
        Task<ComponentActionResult> Start(Components.ComponentTree components);
        Task<ComponentActionResult> Stop();
    }

    public interface ISupplyLogs : IComponent
    {
        Task<ComponentActionResult> Logs();
    }

    public interface IRestorableComponent : IComponent
    {
        Task<ComponentActionResult> Restore();
    }

    public interface IBuildableComponent : IComponent
    {
        Task<ComponentActionResult> Build();
    }

    public interface ITestableComponent : IComponent
    {
        Task<ComponentActionResult> Test();
    }

    public interface IPackageApplication : IComponent
    {
        Task<PackageResult> Package(IImmutableList<PackageResource> resources, string version);
    }

    public interface IHavePackageResources : IComponent
    {
        Task<IImmutableList<PackageResource>> GetPackageResources(
            Components.ComponentTree components,
            string version);
    }

    public interface IHaveHttpEndpoint : IComponent
    {
        string BaseUrl { get; }
        int Port { get; }
    }

    public interface INeedConfiguration : IComponent
    {
        IEnumerable<(string key, string question, ConfigurationType configurationType)> GetRequiredConfigurations();
        
        public enum ConfigurationType
        {
            Project,
            User
        }
    }

    public class PackageResult
    {
        public PackageResult(string packageName, IImmutableList<PackageResource> resources)
        {
            PackageName = packageName;
            Resources = resources;
        }

        public string PackageName { get; }
        public IImmutableList<PackageResource> Resources { get; }
    }

    public class PackageResource
    {
        public PackageResource(string resourceName, byte[] resourceContent)
        {
            ResourceName = resourceName;
            ResourceContent = resourceContent;
        }

        public string ResourceName { get; }
        public byte[] ResourceContent { get; }
    }
}