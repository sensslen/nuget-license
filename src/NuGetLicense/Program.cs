// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using McMaster.Extensions.CommandLineUtils;

namespace NuGetLicense
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication<CommandLineOptions>();

            app.Conventions.UseDefaultConventions();

            try
            {
                return await app.ExecuteAsync(args);
            }
            catch (CommandParsingException ex)
            {
                Console.Error.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
