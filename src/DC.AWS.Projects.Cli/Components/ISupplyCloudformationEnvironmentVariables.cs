using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace DC.AWS.Projects.Cli.Components
{
    public interface ISupplyCloudformationEnvironmentVariables : IComponent
    {
        IImmutableDictionary<string, IImmutableDictionary<string, string>> GetResourceEnvironmentVariables(
            ProjectSettings settings,
            IImmutableDictionary<string, string> variableValues,
            Func<string, string, string> askForValue);
    }
}