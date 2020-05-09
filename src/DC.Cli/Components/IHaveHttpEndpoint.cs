namespace DC.Cli.Components
{
    public interface IHaveHttpEndpoint : IComponent
    {
        string BaseUrl { get; }
        int Port { get; }
    }
}