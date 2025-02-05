// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.SolutionPersistenceWrapper
{
    public class SolutionPersistanceException : Exception
    {
        public SolutionPersistanceException(string message)
            : base(message) { }

        public SolutionPersistanceException(string message, Exception inner)
            : base(message, inner) { }
    }
}
