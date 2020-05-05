using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

namespace DC.AWS.Projects.Cli.Components
{
    public class BuildContext : IBuildContext
    {
        private readonly IList<BuildContext> _children = new Collection<BuildContext>();
        private readonly IList<string> _addedTemplates = new Collection<string>();
        private readonly IList<TemplateData> _addedTemplateData = new Collection<TemplateData>();
        
        private BuildContext(ProjectSettings projectSettings)
        {
            ProjectSettings = projectSettings;
        }

        public ProjectSettings ProjectSettings { get; }
        
        public void AddTemplate(string name)
        {
            if (!_addedTemplates.Contains(name))
                _addedTemplates.Add(name);
        }

        public void ExtendTemplate(TemplateData data)
        {
            _addedTemplateData.Add(data);
        }

        public BuildContext OpenChildContext()
        {
            var child = new BuildContext(ProjectSettings);
            
            _children.Add(child);

            return child;
        }

        public IImmutableDictionary<string, TemplateData> GetTemplates(IImmutableList<string> addedTemplates)
        {
            var allTemplates = addedTemplates.Union(_addedTemplates).ToImmutableList();
            
            var result = new Dictionary<string, TemplateData>();
            
            var thisLevelTemplateData = new TemplateData();

            foreach (var templateData in _addedTemplateData)
                thisLevelTemplateData.Merge(templateData);

            foreach (var child in _children)
            {
                var childTemplates = child.GetTemplates(allTemplates);

                foreach (var childTemplate in childTemplates)
                {
                    if (allTemplates.Contains(childTemplate.Key))
                        thisLevelTemplateData.Merge(childTemplate.Value);
                    else
                        result[childTemplate.Key] = childTemplate.Value;
                }
            }

            foreach (var template in allTemplates)
                result[template] = thisLevelTemplateData;

            return result.ToImmutableDictionary();
        }
        
        public static BuildContext New(ProjectSettings settings)
        {
            return new BuildContext(settings);
        }
    }
}