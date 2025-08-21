﻿// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using McMaster.Extensions.CommandLineUtils;
using NuGetUtility;

namespace NuGetLicenseFramework
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var lifetime = new AppLifetime();
            int returnCode = await CommandLineApplication.ExecuteAsync<NuGetLicense.Program>(args, lifetime.Token);
            lifetime.Done(returnCode);
        }
    }
}
