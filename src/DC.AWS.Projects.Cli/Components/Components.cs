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
        public static ComponentTree FindComponents(ProjectSettings settings, string path)
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
            
            var rootedPath = settings.GetRootedPath(path);

            var directory = new DirectoryInfo(rootedPath);
            
            var components = new List<IComponent>();

            components.AddRange(ApiGatewayComponent.FindAtPath(directory));
            components.AddRange(LambdaFunctionComponent.FindAtPath(directory));
            components.AddRange(ClientComponent.FindAtPath(directory));
            components.AddRange(CloudformationComponent.FindAtPath(directory));
            components.AddRange(LocalProxyComponent.FindAtPath(directory));

            var children = directory
                .GetDirectories()
                .Where(x => !directoriesToIgnore.Any(y => x.Name == y || x.FullName == y || x.FullName.StartsWith(y)))
                .Select(childDirectory => FindComponents(settings, childDirectory.FullName))
                .ToList();

            return new ComponentTree(components.ToImmutableList(), children.ToImmutableList());
        }
        
        public class ComponentTree
        {
            private readonly IImmutableList<IComponent> _components;
            private readonly IImmutableList<ComponentTree> _children;
            
            public ComponentTree(IImmutableList<IComponent> components, IImmutableList<ComponentTree> children)
            {
                _components = components;
                _children = children;
            }

            public async Task<BuildResult> Build(BuildContext context)
            {
                var buildSuccess = true;
                var buildOutput = new StringBuilder();

                foreach (var component in _components)
                {
                    var buildResult = await component.Build(context);

                    if (!buildResult.Success)
                        buildSuccess = false;

                    buildOutput.Append(buildResult.Output);
                }

                var childBuildContext = context.OpenChildContext();

                foreach (var child in _children)
                {
                    var buildResult = await child.Build(childBuildContext);

                    if (!buildResult.Success)
                        buildSuccess = false;

                    buildOutput.Append(buildResult.Output);
                }
                
                return new BuildResult(buildSuccess, buildOutput.ToString());
            }
            
            public async Task<TestResult> Test()
            {
                var testSuccess = true;
                var testOutput = new StringBuilder();

                foreach (var component in _components)
                {
                    var testResult = await component.Test();

                    if (!testResult.Success)
                        testSuccess = false;

                    testOutput.Append(testResult.Output);
                }

                foreach (var child in _children)
                {
                    var testResult = await child.Test();

                    if (!testResult.Success)
                        testSuccess = false;

                    testOutput.Append(testResult.Output);
                }
                
                return new TestResult(testSuccess, testOutput.ToString());
            }
        }
    }
}