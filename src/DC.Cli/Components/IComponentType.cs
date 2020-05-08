using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponentType
    {
        Task<IImmutableList<IComponent>> FindAt(DirectoryInfo path, ProjectSettings settings);
    }
    
    public interface IComponentType<TComponent, in TComponentData> : IComponentType
    {
        Task<TComponent> InitializeAt(Components.ComponentTree tree, TComponentData data, ProjectSettings settings);
    }
}