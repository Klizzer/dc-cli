using System.Collections.Immutable;

namespace DC.AWS.Projects.Cli.Components
{
    public class BuildResult
    {
        public BuildResult(bool success, string output, params IBuildArtifact[] artifacts)
        {
            Success = success;
            Output = output;
            Artifacts = artifacts.ToImmutableList();
        }

        public bool Success { get; }
        public string Output { get; }
        public IImmutableList<IBuildArtifact> Artifacts { get; }
    }

    public interface IBuildArtifact
    {
        
    }

    public class CloudformationNodesArtifact : IBuildArtifact
    {
        
    }
}