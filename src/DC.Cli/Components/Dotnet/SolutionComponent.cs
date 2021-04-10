using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DC.Cli.Components.Dotnet
{
    public class SolutionComponent : IBuildableComponent, 
        ITestableComponent,
        IRestorableComponent,
        ICleanableComponent,
        IPackageApplication
    {
        private readonly FileInfo _path;
        private readonly DirectoryInfo _packagesDirectory;
        private readonly Docker.Container _dockerContainer;

        public SolutionComponent(FileInfo path, ProjectSettings settings)
        {
            _path = path;
            _packagesDirectory = new DirectoryInfo(Path.Combine(path.Directory.FullName, ".packages"));

            _dockerContainer = Docker
                .ContainerFromImage("mcr.microsoft.com/dotnet/sdk:5.0", $"{settings.GetProjectName()}-dotnet-{Name}")
                .EntryPoint("dotnet")
                .WithVolume(path.Directory.FullName, "/usr/local/src", true);
        }

        public string Name => Path.GetFileNameWithoutExtension(_path.Name);
        
        public async Task<IImmutableList<PackageResult>> Package(
            IImmutableList<PackageResource> resources, 
            string version)
        {
            await Clean();
            
            await _dockerContainer.Run($"pack -c Release -o ./.packages -p:Version={version}");

            return _packagesDirectory
                .GetFiles("*.nupkg")
                .Select(x => new PackageResult(x.Name, x.OpenRead()))
                .ToImmutableList();
        }

        public Task<bool> Clean()
        {
            if (_packagesDirectory.Exists)
                _packagesDirectory.Delete();
                
            return _dockerContainer.Run("clean");
        }

        public Task<bool> Restore()
        {
            return _dockerContainer.Run("restore");
        }

        public Task<bool> Test()
        {
            return _dockerContainer.Run("test");
        }

        public Task<bool> Build()
        {
            return _dockerContainer.Run("build");
        }
    }
}