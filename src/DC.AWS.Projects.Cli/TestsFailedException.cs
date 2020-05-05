using System;
using System.Collections.Immutable;

namespace DC.AWS.Projects.Cli
{
    public class TestsFailedException : Exception
    {
        public TestsFailedException(params string[] failureLocations) 
            : base($"Tests failed at locations: {string.Join(", ", failureLocations)}")
        {
            FailureLocations = failureLocations.ToImmutableList();
        }

        public IImmutableList<string> FailureLocations { get; }
    }
}