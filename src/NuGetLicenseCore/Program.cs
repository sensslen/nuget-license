// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetLicenseCore
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await NuGetLicense.Program.Main(args);
        }
    }
}
