namespace DC.AWS.Projects.Cli.Components
{
    public interface IComponentResult
    {
        bool Success { get; }
        string Output { get; }
    }
}