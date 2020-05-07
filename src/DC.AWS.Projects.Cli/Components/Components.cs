using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public static class Components
    {
        public static ComponentTree BuildTree(ProjectSettings settings, string path)
        {
            return BuildTreeFrom(settings, settings.ProjectRoot).Find(path);
        }
        
        private static ComponentTree BuildTreeFrom(ProjectSettings settings, string path)
        {
            var directoriesToIgnore = new List<string>
            {
                "node_modules",
                settings.GetRootedPath("infrastructure"),
                settings.GetRootedPath("config"),
                settings.GetRootedPath("docs"),
                settings.GetRootedPath(".tools"),
                settings.GetRootedPath(".localstack")
            };

            var directory = new DirectoryInfo(settings.GetRootedPath(path));

            var componentTree = GetTreeAt(directory, settings);

            var children = directory
                .GetDirectories()
                .Where(x => !directoriesToIgnore.Any(y => x.Name == y || x.FullName == y || x.FullName.StartsWith(y)))
                .Select(childDirectory => BuildTreeFrom(settings, childDirectory.FullName))
                .ToImmutableList();

            return ComponentTree.WithChildren(componentTree, children);
        }

        private static ComponentTree GetTreeAt(DirectoryInfo directory, ProjectSettings settings)
        {
            var components = new List<IComponent>();

            components.AddRange(ApiGatewayComponent.FindAtPath(directory, settings));
            components.AddRange(LambdaFunctionComponent.FindAtPath(directory));
            components.AddRange(ClientComponent.FindAtPath(directory));
            components.AddRange(CloudformationComponent.FindAtPath(directory));
            components.AddRange(LocalProxyComponent.FindAtPath(directory));
            components.AddRange(CloudformationStackComponent.FindAtPath(directory, settings));
            components.AddRange(TerraformRootComponent.FindAtPath(directory));

            return new ComponentTree(components.ToImmutableList(), directory);
        }

        public class ComponentTree
        {
            public ComponentTree(IImmutableList<IComponent> components, DirectoryInfo path)
            {
                Components = components;
                Path = path;
            }

            public ComponentTree Parent { get; private set; }
            public IImmutableList<IComponent> Components { get; }
            public IImmutableList<ComponentTree> Children { get; private set; }
            public DirectoryInfo Path { get; }
            
            public static ComponentTree WithChildren(ComponentTree tree, IImmutableList<ComponentTree> children)
            {
                tree.Children = children;

                foreach (var child in children)
                    child.Parent = tree;

                return tree;
            }
            
            public Task<ComponentActionResult> Build()
            {
                return Run<IBuildableComponent>((component, _) => component.Build());
            }

            public Task<ComponentActionResult> Test()
            {
                return Run<ITestableComponent>((component, _) => component.Test());
            }

            public Task<ComponentActionResult> Restore()
            {
                return Run<IRestorableComponent>((component, _) => component.Restore());
            }

            public Task<ComponentActionResult> Start()
            {
                return Run<IStartableComponent>((component, tree) => component.Start(tree));
            }

            public Task<ComponentActionResult> Stop()
            {
                return Run<IStartableComponent>((component, _) => component.Stop());
            }

            public Task<ComponentActionResult> Logs()
            {
                return Run<ISupplyLogs>((component, tree) => component.Logs());
            }

            public async Task<IImmutableList<PackageResult>> Package(string version)
            {
                var results = new List<PackageResult>();

                foreach (var package in Components.OfType<IPackageApplication>())
                {
                    var resources = (await FindAll<IHavePackageResources>(Direction.In)
                            .Select(x => x.component.GetPackageResources(this, version))
                            .WhenAll())
                        .SelectMany(x => x)
                        .ToImmutableList();

                    var result = await package.Package(resources, version);
                    
                    results.Add(result);
                }

                foreach (var child in Children)
                    results.AddRange(await child.Package(version));

                return results.ToImmutableList();
            }

            private async Task<ComponentActionResult> Run<TComponentType>(
                Func<TComponentType, ComponentTree, Task<ComponentActionResult>> execute) 
                where TComponentType : IComponent
            {
                var success = true;
                var output = new StringBuilder();

                foreach (var component in Components.OfType<TComponentType>())
                {
                    var result = await execute(component, this);

                    if (!result.Success)
                        success = false;

                    output.Append(result.Output);
                }

                foreach (var child in Children)
                {
                    var result = await child.Run(execute);

                    if (!result.Success)
                        success = false;

                    output.Append(result.Output);
                }

                return new ComponentActionResult(success, output.ToString());
            }

            public ComponentTree Find(string path)
            {
                if (Path.FullName == path)
                    return this;

                return Children
                    .Select(x => x.Find(path))
                    .FirstOrDefault(x => x != null);
            }

            public TComponent FindFirst<TComponent>(Direction direction, string name = null)
                where TComponent : class, IComponent
            {
                switch (direction)
                {
                    case Direction.In:
                        var queue = new Queue<ComponentTree>();

                        queue.Enqueue(this);

                        while (queue.Count > 0)
                        {
                            var tree = queue.Dequeue();

                            var matchingComponent = tree.FindComponent<TComponent>();

                            if (matchingComponent != null)
                                return matchingComponent;

                            foreach (var child in tree.Children)
                                queue.Enqueue(child);
                        }

                        return null;

                    case Direction.Out:
                        return FindComponent<TComponent>(name) ?? Parent?.FindComponent<TComponent>(name);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }
            }

            public IImmutableList<(ComponentTree tree, TComponent component)> FindAll<TComponent>(Direction direction)
                where TComponent : IComponent
            {
                var result = Components
                    .OfType<TComponent>()
                    .Select(x => (this, x))
                    .ToList();

                switch (direction)
                {
                    case Direction.In:
                        foreach (var child in Children)
                            result.AddRange(child.FindAll<TComponent>(direction));
                        
                        break;
                    case Direction.Out:
                        result.AddRange(Parent?.FindAll<TComponent>(direction) ??
                                        Enumerable.Empty<(ComponentTree, TComponent)>());
                        
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }

                return result.ToImmutableList();
            }

            public TComponent FindComponent<TComponent>(string name = null) where TComponent : class, IComponent
            {
                return Components
                    .OfType<TComponent>()
                    .FirstOrDefault(x => string.IsNullOrEmpty(name) || x.Name == name);
            }
        }

        public enum Direction
        {
            In,
            Out
        }
    }
}