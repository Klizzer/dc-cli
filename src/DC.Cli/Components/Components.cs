using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DC.Cli.Components.Aws.ApiGateway;
using DC.Cli.Components.Aws.ChildConfig;
using DC.Cli.Components.Aws.CloudformationStack;
using DC.Cli.Components.Aws.CloudformationTemplate;
using DC.Cli.Components.Aws.LambdaFunction;
using DC.Cli.Components.Aws.LambdaLayer;
using DC.Cli.Components.Client;
using DC.Cli.Components.Cloudflare;
using DC.Cli.Components.PackageFiles;
using DC.Cli.Components.Powershell;
using DC.Cli.Components.Terraform;
using MAB.DotIgnore;

namespace DC.Cli.Components
{
    public static class Components
    {
        private static readonly IImmutableList<IComponentType> ComponentTypes = new List<IComponentType>
        {
            new ApiGatewayComponentType(),
            new LambdaFunctionComponentType(),
            new ClientComponentType(),
            new CloudformationComponentType(),
            new CloudformationStackComponentType(),
            new TerraformResourceComponentType(),
            new TerraformRootComponentType(),
            new CloudflareWorkerComponentType(),
            new PowershellScriptComponentType(),
            new LambdaLayerComponentType(),
            new ChildConfigComponentType(),
            new PackageFileComponentType()
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
            
            var tree = new ComponentTree(directory);

            foreach (var componentType in ComponentTypes)
            {
                components.AddRange(await componentType.FindAt(tree, settings));
            }

            return ComponentTree.WithComponents(tree, components.ToImmutableList());
        }

        public class ComponentTree
        {
            private ComponentTree _parent;
            private IImmutableList<IComponent> _components = ImmutableList<IComponent>.Empty;
            private IImmutableList<ComponentTree> _children = ImmutableList<ComponentTree>.Empty;
            
            public ComponentTree(DirectoryInfo path)
            {
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

            public static ComponentTree WithComponents(ComponentTree tree, IImmutableList<IComponent> components)
            {
                tree._components = tree._components.AddRange(components);

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
                    await Configure(settings, false, false);

                    await settings.Save();
                }

                foreach (var restorableComponent in newComponents.OfType<IRestorableComponent>())
                    await restorableComponent.Restore();
            }

            public async Task Configure(ProjectSettings settings, bool recursive, bool overwrite)
            {
                var newConfigurations = new Dictionary<string, (string value, INeedConfiguration.ConfigurationType configurationType)>();

                var requiredConfigurations = (await _components
                        .OfType<INeedConfiguration>()
                        .Select(x => x.GetRequiredConfigurations(this))
                        .WhenAll())
                    .SelectMany(x => x);

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
                    await child.Configure(settings, true, overwrite);
            }
            
            public Task<bool> Build()
            {
                return MergeResults(Run<IBuildableComponent>((component, _) => component.Build(), "Build"));
            }

            public Task<bool> Test()
            {
                return MergeResults(Run<ITestableComponent>((component, _) => component.Test(), "Test"));
            }

            public Task<bool> Restore()
            {
                return MergeResults(Run<IRestorableComponent>((component, _) => component.Restore(), "Restore"));
            }
            
            public Task<bool> Clean()
            {
                return MergeResults(Run<ICleanableComponent>((component, _) => component.Clean(), "Clean"));
            }

            public Task<bool> Start()
            {
                return MergeResults(Run<IStartableComponent>((component, tree) => component.Start(tree), "Start"));
            }

            public Task<bool> Stop()
            {
                return MergeResults(Run<IStartableComponent>((component, _) => component.Stop(), "Stop"));
            }

            public Task<bool> Logs()
            {
                return MergeResults(Run<IComponentWithLogs>((component, tree) => component.Logs(), "Logs"));
            }

            public async Task<IImmutableList<PackageResult>> Package(string version)
            {
                var results = new List<PackageResult>();

                foreach (var package in _components.OfType<IPackageApplication>())
                {
                    var resources = (await FindAll<IHavePackageResources>(Direction.In)
                            .Select(x => x.component?.GetPackageResources(this, version))
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

            private IEnumerable<Task<bool>> Run<TComponentType>(
                Func<TComponentType, ComponentTree, Task<bool>> execute,
                string actionName)
                where TComponentType : IComponent
            {
                foreach (var component in _components.OfType<TComponentType>())
                {
                    yield return Retries.RetryOnException(
                        () => execute(component, this),
                        $"{component.Name}.{actionName}");
                }

                foreach (var child in _children)
                {
                    foreach (var childRun in child.Run(execute, actionName))
                        yield return childRun;
                }
            }

            private static async Task<bool> MergeResults(
                IEnumerable<Task<bool>> resultCollectors)
            {
                var results = await Task.WhenAll(resultCollectors);

                return results.All(x => x);
            }

            public ComponentTree Find(string path)
            {
                if (Path.FullName == path)
                    return this;

                return _children
                    .Select(x => x.Find(path))
                    .FirstOrDefault(x => x != null);
            }

            public FoundComponent<TComponent> FindFirst<TComponent>(Direction direction, string name = null)
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
                                return new FoundComponent<TComponent>(tree, matchingComponent);

                            foreach (var child in tree._children)
                                queue.Enqueue(child);
                        }

                        return null;

                    case Direction.Out:
                        var component = FindComponent<TComponent>(name);

                        return component != null
                            ? new FoundComponent<TComponent>(this, component)
                            : _parent?.FindFirst<TComponent>(Direction.Out, name);

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
        
        public class FoundComponent<TComponent>
        {
            public FoundComponent(ComponentTree foundAt, TComponent component)
            {
                FoundAt = foundAt;
                Component = component;
            }

            public ComponentTree FoundAt { get; }
            public TComponent Component { get; }
        }
    }
}