using System.IO;
using System.Threading.Tasks;

namespace DC.Cli.Components.Dotnet
{
    public class SolutionComponent : IBuildableComponent, 
        ITestableComponent,
        IRestorableComponent,
        ICleanableComponent
    {
        private readonly FileInfo _path;
        private readonly Docker.Container _dockerContainer;

        public SolutionComponent(FileInfo path, ProjectSettings settings)
        {
            _path = path;
            
            _dockerContainer = Docker
                .ContainerFromImage("mcr.microsoft.com/dotnet/sdk:5.0", $"{settings.GetProjectName()}-dotnet-{Name}")
                .EntryPoint("dotnet")
                .WithVolume(path.Directory.FullName, "/usr/local/src", true);
        }

        public string Name => Path.GetFileNameWithoutExtension(_path.Name);
        
        public Task<bool> Clean()
        {
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
            return _dockerContainer.Run("build -c Release");
        }
    }
}