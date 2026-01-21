// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.CommandLine;
using NuGetUtility;

namespace NuGetLicenseCore
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var lifetime = new AppLifetime();
            var rootCommand = NuGetLicense.Program.CreateRootCommand();
            int returnCode = await rootCommand.InvokeAsync(args);
            lifetime.Done(returnCode);
        }
    }
}
