namespace DC.AWS.Projects.Cli
{
    public interface ILanguageRuntime
    {
        string Language { get; }
        string Name { get; }

        void Build(string path);
        bool Test(string path);

        string GetHandlerName();
        string GetFunctionOutputPath(string functionPath);
    }
}