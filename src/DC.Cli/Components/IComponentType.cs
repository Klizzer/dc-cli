using System.Collections.Immutable;
using System.Threading.Tasks;

namespace DC.Cli.Components
{
    public interface IComponentType
    {
        Task<IImmutableList<IComponent>> FindAt(Components.ComponentTree components, ProjectSettings settings);
    }
    
    public interface IComponentType<TComponent, in TComponentData> : IComponentType
    {
        Task<TComponent> InitializeAt(Components.ComponentTree tree, TComponentData data, ProjectSettings settings);
    }
}