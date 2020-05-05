namespace DC.AWS.Projects.Cli.Components
{
    public interface IBuildContext
    {
        ProjectSettings ProjectSettings { get; }
        void AddTemplate(string name);
        void ExtendTemplate(TemplateData data);
    }
}