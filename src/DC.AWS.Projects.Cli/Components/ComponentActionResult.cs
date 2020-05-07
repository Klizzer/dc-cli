namespace DC.AWS.Projects.Cli.Components
{
    public class ComponentActionResult
    {
        public ComponentActionResult(bool success, string output)
        {
            Success = success;
            Output = output;
        }

        public bool Success { get; }
        public string Output { get; }
    }
}