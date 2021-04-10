using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace DC.Cli.Components.Dotnet
{
    public class SolutionComponentType : IComponentType<SolutionComponent, SolutionComponentType.ComponentData>
    {
        public Task<SolutionComponent> InitializeAt(
            Components.ComponentTree tree,
            ComponentData data,
            ProjectSettings settings)
        {
            throw new System.NotImplementedException();
        }

        public Task<IImmutableList<IComponent>> FindAt(Components.ComponentTree components, ProjectSettings settings)
        {
            var result = from file in components.Path.EnumerateFiles()
                where file.Name.EndsWith(".sln")
                select new SolutionComponent(file, settings);
            
            return Task.FromResult<IImmutableList<IComponent>>(result.OfType<IComponent>().ToImmutableList());
        }

        public class ComponentData
        {
            
        }
    }
}