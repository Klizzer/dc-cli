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

            var componentTree = GetTreeAt(directory);

            var children = directory
                .GetDirectories()
                .Where(x => !directoriesToIgnore.Any(y => x.Name == y || x.FullName == y || x.FullName.StartsWith(y)))
                .Select(childDirectory => BuildTreeFrom(settings, childDirectory.FullName))
                .ToImmutableList();

            return ComponentTree.WithChildren(componentTree, children);
        }

        private static ComponentTree GetTreeAt(DirectoryInfo directory)
        {
            var components = new List<IComponent>();

            components.AddRange(ApiGatewayComponent.FindAtPath(directory));
            components.AddRange(LambdaFunctionComponent.FindAtPath(directory));
            components.AddRange(ClientComponent.FindAtPath(directory));
            components.AddRange(CloudformationComponent.FindAtPath(directory));
            components.AddRange(LocalProxyComponent.FindAtPath(directory));

            return new ComponentTree(components.ToImmutableList(), directory.FullName);
        }

        public class ComponentTree
        {
            private readonly string _path;
            
            public ComponentTree(IImmutableList<IComponent> components, string path)
            {
                Components = components;
                _path = path;
            }

            public ComponentTree Parent { get; private set; }
            public IImmutableList<IComponent> Components { get; }
            public IImmutableList<ComponentTree> Children { get; private set; }
            
            public static ComponentTree WithChildren(ComponentTree tree, IImmutableList<ComponentTree> children)
            {
                tree.Children = children;

                foreach (var child in children)
                    child.Parent = tree;

                return tree;
            }
            
            public Task<BuildResult> Build(BuildContext context)
            {
                return Run<BuildResult, BuildContext>(
                    context,
                    (ctx, component) => component.Build(ctx),
                    ctx => ctx.OpenChildContext(),
                    (success, output) => new BuildResult(success, output));
            }

            public Task<TestResult> Test()
            {
                return Run<TestResult, object>(
                    null,
                    (_, component) => component.Test(),
                    _ => null,
                    (success, output) => new TestResult(success, output));
            }

            public Task<RestoreResult> Restore()
            {
                return Run<RestoreResult, object>(
                    null,
                    (_, component) => component.Restore(),
                    _ => null,
                    (success, output) => new RestoreResult(success, output));
            }

            private async Task<TResult> Run<TResult, TContext>(
                TContext context,
                Func<TContext, IComponent, Task<TResult>> execute,
                Func<TContext, TContext> openChildContext,
                Func<bool, string, TResult> getResult) 
                where TResult : IComponentResult
            {
                var success = true;
                var output = new StringBuilder();

                foreach (var component in Components)
                {
                    var result = await execute(context, component);

                    if (!result.Success)
                        success = false;

                    output.Append(result.Output);
                }

                var childContext = openChildContext(context);

                foreach (var child in Children)
                {
                    var result = await child.Run(childContext, execute, openChildContext, getResult);

                    if (!result.Success)
                        success = false;

                    output.Append(result.Output);
                }

                return getResult(success, output.ToString());
            }

            public ComponentTree Find(string path)
            {
                if (_path == path)
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

            private TComponent FindComponent<TComponent>(string name = null) where TComponent : class, IComponent
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