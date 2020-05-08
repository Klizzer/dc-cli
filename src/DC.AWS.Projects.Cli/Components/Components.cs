using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DC.AWS.Projects.Cli.Components.Aws.ApiGateway;
using DC.AWS.Projects.Cli.Components.Aws.CloudformationStack;
using DC.AWS.Projects.Cli.Components.Aws.CloudformationTemplate;
using DC.AWS.Projects.Cli.Components.Aws.LambdaFunction;
using DC.AWS.Projects.Cli.Components.Client;
using DC.AWS.Projects.Cli.Components.Nginx;
using DC.AWS.Projects.Cli.Components.Terraform;
using MAB.DotIgnore;

namespace DC.AWS.Projects.Cli.Components
{
    public static class Components
    {
        private static readonly IImmutableList<IComponentType> ComponentTypes = new List<IComponentType>
        {
            new ApiGatewayComponentType(),
            new LambdaFunctionComponentType(),
            new ClientComponentType(),
            new CloudformationComponentType(),
            new LocalProxyComponentType(),
            new CloudformationStackComponentType(),
            new TerraformResourceComponentType(),
            new TerraformResourceComponentType()
        }.ToImmutableList();
        
        public static async Task<ComponentTree> BuildTree(ProjectSettings settings, string path)
        {
            var dir = new DirectoryInfo(settings.GetRootedPath(path));
            
            if (!dir.Exists)
                dir.Create();

            var ignoreFile = settings.GetRootedPath(".dcignore");
            var ignores = new List<string>();

            if (File.Exists(ignoreFile))
                ignores = (await File.ReadAllLinesAsync(ignoreFile)).ToList();
            
            var ignoreList = new IgnoreList(ignores);
            
            return (await BuildTreeFrom(settings, settings.ProjectRoot, ignoreList)).Find(dir.FullName);
        }
        
        private static async Task<ComponentTree> BuildTreeFrom(
            ProjectSettings settings,
            string path,
            IgnoreList ignoreList)
        {
            var directory = new DirectoryInfo(settings.GetRootedPath(path));

            var componentTree = await GetTreeAt(directory, settings);

            var children = (await directory
                .GetDirectories()
                .Where(x => !ignoreList.IsIgnored(new DirectoryInfo(settings.GetRelativePath(x.FullName))))
                .Select(childDirectory => BuildTreeFrom(settings, childDirectory.FullName, ignoreList))
                .WhenAll())
                .ToImmutableList();

            return ComponentTree.WithChildren(componentTree, children);
        }

        private static async Task<ComponentTree> GetTreeAt(DirectoryInfo directory, ProjectSettings settings)
        {
            var components = new List<IComponent>();

            foreach (var componentType in ComponentTypes)
            {
                components.AddRange(await componentType.FindAt(directory, settings));
            }

            return new ComponentTree(components.ToImmutableList(), directory);
        }

        public class ComponentTree
        {
            private ComponentTree _parent;
            private IImmutableList<IComponent> _components;
            private IImmutableList<ComponentTree> _children;
            
            public ComponentTree(IImmutableList<IComponent> components, DirectoryInfo path)
            {
                _components = components;
                Path = path;
            }
            
            public DirectoryInfo Path { get; }
            
            public static ComponentTree WithChildren(ComponentTree tree, IImmutableList<ComponentTree> children)
            {
                tree._children = children;

                foreach (var child in children)
                    child._parent = tree;

                return tree;
            }

            public async Task Initialize<TComponent, TComponentData>(TComponentData data, ProjectSettings settings)
                where TComponent : IComponent
            {
                var componentType = ComponentTypes
                    .OfType<IComponentType<TComponent, TComponentData>>()
                    .ToList();
                
                var newComponents = new List<IComponent>();

                foreach (var type in componentType)
                {
                    var component = await type.InitializeAt(this, data, settings);
                    
                    if (component != null)
                        newComponents.Add(component);
                }

                _components = _components.AddRange(newComponents);

                if (newComponents.OfType<INeedConfiguration>().Any())
                {
                    Configure(settings, false, false);

                    await settings.Save();
                }
            }

            public void Configure(ProjectSettings settings, bool recursive, bool overwrite)
            {
                var newConfigurations = new Dictionary<string, (string value, INeedConfiguration.ConfigurationType configurationType)>();

                var requiredConfigurations = _components
                    .OfType<INeedConfiguration>()
                    .SelectMany(x => x.GetRequiredConfigurations())
                    .ToImmutableList();

                foreach (var requiredConfiguration in requiredConfigurations)
                {
                    if (newConfigurations.ContainsKey(requiredConfiguration.key))
                    {
                        if (requiredConfiguration.configurationType <
                            newConfigurations[requiredConfiguration.key].configurationType)
                        {
                            newConfigurations[requiredConfiguration.key] = (
                                newConfigurations[requiredConfiguration.key].value,
                                requiredConfiguration.configurationType);
                        }
                    
                        continue;
                    }
                
                    if (settings.HasConfiguration(requiredConfiguration.key) && !overwrite)
                        continue;

                    var value = ConsoleInput.Ask(requiredConfiguration.question);

                    newConfigurations[requiredConfiguration.key] = (value, requiredConfiguration.configurationType);
                }

                foreach (var newConfiguration in newConfigurations)
                {
                    settings.SetConfiguration(newConfiguration.Key, newConfiguration.Value.value,
                        newConfiguration.Value.configurationType);
                }

                if (!recursive) 
                    return;
                
                foreach (var child in _children)
                    child.Configure(settings, true, overwrite);
            }
            
            public Task<ComponentActionResult> Build()
            {
                return MergeResults(Run<IBuildableComponent>((component, _) => component.Build()));
            }

            public Task<ComponentActionResult> Test()
            {
                return MergeResults(Run<ITestableComponent>((component, _) => component.Test()));
            }

            public Task<ComponentActionResult> Restore()
            {
                return MergeResults(Run<IRestorableComponent>((component, _) => component.Restore()));
            }

            public Task<ComponentActionResult> Start()
            {
                return MergeResults(Run<IStartableComponent>((component, tree) => component.Start(tree)));
            }

            public Task<ComponentActionResult> Stop()
            {
                return MergeResults(Run<IStartableComponent>((component, _) => component.Stop()));
            }

            public Task<ComponentActionResult> Logs()
            {
                return MergeResults(Run<IComponentWithLogs>((component, tree) => component.Logs()));
            }

            public async Task<IImmutableList<PackageResult>> Package(string version)
            {
                var results = new List<PackageResult>();

                foreach (var package in _components.OfType<IPackageApplication>())
                {
                    var resources = (await FindAll<IHavePackageResources>(Direction.In)
                            .Select(x => x.component.GetPackageResources(this, version))
                            .WhenAll())
                        .SelectMany(x => x)
                        .ToImmutableList();

                    var result = await package.Package(resources, version);
                    
                    results.Add(result);
                }

                foreach (var child in _children)
                    results.AddRange(await child.Package(version));

                return results.ToImmutableList();
            }

            private IEnumerable<Task<ComponentActionResult>> Run<TComponentType>(
                Func<TComponentType, ComponentTree, Task<ComponentActionResult>> execute) 
                where TComponentType : IComponent
            {
                foreach (var component in _components.OfType<TComponentType>())
                    yield return execute(component, this);

                foreach (var child in _children)
                {
                    foreach (var childRun in child.Run(execute))
                        yield return childRun;
                }
            }

            private async Task<ComponentActionResult> MergeResults(
                IEnumerable<Task<ComponentActionResult>> resultCollectors)
            {
                var results = await Task.WhenAll(resultCollectors);
                
                return new ComponentActionResult(
                    results.All(x => x.Success),
                    string.Join("\n", results.Select(x => x.Output)));
            }

            public ComponentTree Find(string path)
            {
                if (Path.FullName == path)
                    return this;

                return _children
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

                            foreach (var child in tree._children)
                                queue.Enqueue(child);
                        }

                        return null;

                    case Direction.Out:
                        return FindComponent<TComponent>(name) ?? _parent?.FindComponent<TComponent>(name);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }
            }

            public IImmutableList<(ComponentTree tree, TComponent component)> FindAll<TComponent>(Direction direction)
                where TComponent : IComponent
            {
                var result = _components
                    .OfType<TComponent>()
                    .Select(x => (this, x))
                    .ToList();

                switch (direction)
                {
                    case Direction.In:
                        foreach (var child in _children)
                            result.AddRange(child.FindAll<TComponent>(direction));
                        
                        break;
                    case Direction.Out:
                        result.AddRange(_parent?.FindAll<TComponent>(direction) ??
                                        Enumerable.Empty<(ComponentTree, TComponent)>());
                        
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }

                return result.ToImmutableList();
            }

            public IImmutableList<(ComponentTree tree, TComponent component)> FindAllFirstLevel<TComponent>()
            {
                var result = _components
                    .OfType<TComponent>()
                    .Select(x => (this, x))
                    .ToList();

                if (result.Any())
                    return result.ToImmutableList();

                foreach (var child in _children)
                    result.AddRange(child.FindAllFirstLevel<TComponent>());

                return result.ToImmutableList();
            }

            private TComponent FindComponent<TComponent>(string name = null) where TComponent : class, IComponent
            {
                return _components
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