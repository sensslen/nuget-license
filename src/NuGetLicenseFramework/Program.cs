// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.CommandLine;

namespace NuGetLicenseFramework
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            RootCommand rootCommand = NuGetLicense.Program.CreateRootCommand();
            ParseResult parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }
    }
}
