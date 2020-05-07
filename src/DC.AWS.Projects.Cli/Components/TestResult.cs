namespace DC.AWS.Projects.Cli.Components
{
    public class TestResult : IComponentResult
    {
        public TestResult(bool success, string output)
        {
            Success = success;
            Output = output;
        }

        public bool Success { get; }
        public string Output { get; }
    }
}