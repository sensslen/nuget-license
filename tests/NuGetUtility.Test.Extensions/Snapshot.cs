// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Globalization;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TUnit.Core;

namespace NuGetUtility.Test.Extensions
{
    /// <summary>
    /// Compares a test's output against a committed snapshot stored next to the calling source file.
    /// </summary>
    /// <remarks>
    /// The file name carries a hash of the test's class and method arguments rather than the arguments
    /// themselves, because the output formatter matrices would otherwise exceed the maximum path length.
    /// To accept new output, run the tests and rename the .received.txt files that mismatches leave behind.
    /// </remarks>
    public static class Snapshot
    {
        /// <param name="suffix">Distinguishes snapshots of the same test that legitimately differ, such as <see cref="OperatingSystem" />.</param>
        /// <param name="comparer">Replaces the default exact comparison. Receives the received value first, then the verified one.</param>
        public static void Verify(string received,
            string? suffix = null,
            Func<string, string, bool>? comparer = null,
            [CallerFilePath] string sourceFile = "")
        {
            TestContext context = TestContext.Current ??
                                  throw new InvalidOperationException($"{nameof(Snapshot)}.{nameof(Verify)} can only be called from within a test.");

            string snapshot = Path.Combine(Path.GetDirectoryName(sourceFile)!, BuildName(context.Metadata.TestDetails, suffix));
            string verifiedFile = $"{snapshot}.verified.txt";
            string receivedFile = $"{snapshot}.received.txt";

            // The snapshots are stored with unix line endings so that they compare equal across platforms.
            string normalized = received.Replace("\r\n", "\n").Replace("\r", "\n");

            if (File.Exists(verifiedFile))
            {
                string verified = File.ReadAllText(verifiedFile);
                bool matches = comparer is null ? string.Equals(normalized, verified, StringComparison.Ordinal) : comparer(normalized, verified);
                if (matches)
                {
                    File.Delete(receivedFile);
                    return;
                }
            }

            File.WriteAllText(receivedFile, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            throw new InvalidOperationException($"The received value does not match the snapshot.\nVerified: {verifiedFile}\nReceived: {receivedFile}");
        }

        /// <summary>The platform the test runs on, for snapshots whose content is platform specific.</summary>
        public static string OperatingSystem { get; } = GetOperatingSystem();

        private static string BuildName(TestDetails details, string? suffix)
        {
            var name = new StringBuilder(details.MethodMetadata.Class.Type.Name)
                .Append('.')
                .Append(details.MethodMetadata.Name);

            string?[] parameterNames = [.. details.MethodMetadata.Class.Parameters.Select(p => p.Name), .. details.MethodMetadata.Parameters.Select(p => p.Name)];
            object?[] arguments = [.. details.TestClassArguments, .. details.TestMethodArguments];
            if (arguments.Length > 0)
            {
                name.Append('_').Append(HashArguments(parameterNames, arguments));
            }

            if (suffix is not null)
            {
                name.Append('.').Append(suffix);
            }

            return name.ToString();
        }

        private static string HashArguments(string?[] parameterNames, object?[] arguments)
        {
            if (parameterNames.Length != arguments.Length)
            {
                throw new InvalidOperationException($"The test declares {parameterNames.Length} parameters but was given {arguments.Length} arguments. Snapshot names would not be stable.");
            }

            var builder = new StringBuilder();
            for (int i = 0; i < arguments.Length; i++)
            {
                builder.Append('_').Append(parameterNames[i]).Append('=').Append(Format(arguments[i]));
            }

            return ToHex(XxHash64.Hash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static string Format(object? argument) => argument switch
        {
            null => "null",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => argument.ToString() ?? string.Empty,
        };

        private static string ToHex(byte[] hash)
        {
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        private static string GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "OSX";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            throw new PlatformNotSupportedException($"Unknown operating system: {RuntimeInformation.OSDescription}");
        }
    }
}
