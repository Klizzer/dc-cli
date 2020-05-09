using System.Collections.Generic;

namespace DC.Cli.Components
{
    public interface INeedConfiguration : IComponent
    {
        IEnumerable<(string key, string question, ConfigurationType configurationType)> GetRequiredConfigurations();
        
        public enum ConfigurationType
        {
            Project,
            User
        }
    }
}