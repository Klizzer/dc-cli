namespace DC.AWS.Projects.Cli.Components
{
    public class RestoreResult : IComponentResult
    {
        public RestoreResult(bool success, string output)
        {
            Success = success;
            Output = output;
        }

        public bool Success { get; }
        public string Output { get; }
    }
}