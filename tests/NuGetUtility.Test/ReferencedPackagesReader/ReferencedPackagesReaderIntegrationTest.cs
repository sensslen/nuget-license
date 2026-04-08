// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Runtime.InteropServices;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;

namespace NuGetUtility.Test.ReferencedPackagesReader
{
    public class ReferencedPackagesReaderIntegrationTest
    {
        private readonly ReferencedPackageReader? _uut;
        private readonly bool _canRun;

        public ReferencedPackagesReaderIntegrationTest()
        {
#if NETFRAMEWORK
            IPackagesConfigReader packagesConfigReader = new WindowsPackagesConfigReader();
#else
            IPackagesConfigReader packagesConfigReader = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new WindowsPackagesConfigReader() : new FailingPackagesConfigReader();
#endif

            try
            {
                _uut = new ReferencedPackageReader(
                    new MsBuildAbstraction(),
                    new LockFileFactory(),
                    new NuGetFrameworkUtility(),
                    new AssetsPackageDependencyReader(new NuGetFrameworkUtility()),
                    packagesConfigReader);
                _canRun = true;
            }
            catch
            {
                _canRun = false;
            }
        }

        private bool CannotRun() => !_canRun || _uut is null;

        [Test]
        public async Task GetInstalledPackagesShould_ReturnPackagesForActualProjectCorrectly()
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/PackageReferenceProject/PackageReferenceProject.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, false);

            await Assert.That(result.Count()).IsEqualTo(1);
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnTransitivePackages()
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath(
                "../../../../targets/ProjectWithTransitiveReferences/ProjectWithTransitiveReferences.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, true);

            await Assert.That(result.Count()).IsEqualTo(2);
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnTransitiveNuGet()
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath(
                "../../../../targets/ProjectWithTransitiveNuget/ProjectWithTransitiveNuget.csproj");

            PackageIdentity[] result = _uut!.GetInstalledPackages(path, true).ToArray();

            await Assert.That(result.Length).IsEqualTo(3);
            string[] titles = result.Select(metadata => metadata.Id).ToArray();
            await Assert.That(titles.Contains("NSubstitute")).IsTrue();
            await Assert.That(titles.Contains("Castle.Core")).IsTrue();
            await Assert.That(titles.Contains("System.Diagnostics.EventLog")).IsTrue();
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnEmptyEnumerable_For_ProjectsWithoutPackages()
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath(
                "../../../../targets/ProjectWithoutNugetReferences/ProjectWithoutNugetReferences.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, false);

            await Assert.That(result.Count()).IsEqualTo(0);
        }

        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task GetInstalledPackagesShould_ReturnResolvedDependency_For_ProjectWithRangedDependencies(bool includeTransitive)
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath(
                "../../../../targets/VersionRangesProject/VersionRangesProject.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, includeTransitive);

            await Assert.That(result.Count()).IsEqualTo(includeTransitive ? 3 : 1);
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnPackages_For_PackagesConfigProject()
        {
            if (CannotRun() || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/PackagesConfigProject/PackagesConfigProject.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, false);

            await Assert.That(result.Count()).IsEqualTo(1);
        }

        [Test]
        public async Task GetInstalledPackagesShould_ThrowError_PackagesConfigProject()
        {
            if (CannotRun() || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/PackagesConfigProject/PackagesConfigProject.csproj");

            await Assert.That(() => _uut!.GetInstalledPackages(path, false))
                .Throws<PackagesConfigReaderException>()
                .WithMessage($"Invalid project structure detected. Currently packages.config projects are only supported on Windows (Project: {path})");
        }

#if NETFRAMEWORK
        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task GetInstalledPackagesShould_ReturnPackages_For_NativeCppProject_With_References(bool includeTransitive)
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/SimpleCppProject/SimpleCppProject.vcxproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, includeTransitive);

            await Assert.That(result.Count()).IsEqualTo(2);
        }
#else
        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task GetInstalledPackagesShould_ThrowError_For_PackagesForNativeCppProject_With_References(bool includeTransitive)
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/SimpleCppProject/SimpleCppProject.vcxproj");

            await Assert.That(() => _uut!.GetInstalledPackages(path, includeTransitive))
                .Throws<MsBuildAbstractionException>()
                .WithMessage($"Please use the .net Framework version to analyze c++ projects (Project: {path})");
        }
#endif

#if NETFRAMEWORK
        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task GetInstalledPackagesShould_ReturnPackages_For_NativeCppProject_Without_References(bool includeTransitive)
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/EmptyCppProject/EmptyCppProject.vcxproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, includeTransitive);

            await Assert.That(result.Count()).IsEqualTo(0);
        }
#else
        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task GetInstalledPackagesShould_ThrowError_For_PackagesForNativeCppProject_Without_References(bool includeTransitive)
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/EmptyCppProject/EmptyCppProject.vcxproj");

            await Assert.That(() => _uut!.GetInstalledPackages(path, includeTransitive))
                .Throws<MsBuildAbstractionException>()
                .WithMessage($"Please use the .net Framework version to analyze c++ projects (Project: {path})");
        }
#endif

        [Test]
        [Arguments("net9.0", false, new[] { "TinyCsvParser" })]
        [Arguments("net8.0", false, new[] { "Microsoft.Extensions.Logging.Abstractions" })]
        [Arguments("net8.0-browser", false, new[] { "Microsoft.Extensions.Logging.Abstractions" })]
        [Arguments("net9.0", true, new[] { "System.IO.Pipelines", "TinyCsvParser" })]
        [Arguments("net8.0", true, new[] { "Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.DependencyInjection.Abstractions", "System.Diagnostics.DiagnosticSource" })]
        [Arguments("net8.0-browser", true, new[] { "Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.DependencyInjection.Abstractions", "System.Diagnostics.DiagnosticSource" })]
        public async Task GetInstalledPackagesShould_OnlyReturn_PackagesPackagesReferencedByRequestedFramework(string framework, bool includeTransitive, string[] packages)
        {
            if (CannotRun())
            {
                return;
            }

            string path = Path.GetFullPath("../../../../targets/MultiTargetProjectWithDifferentDependencies/MultiTargetProjectWithDifferentDependencies.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, includeTransitive, framework);

            await Assert.That(result.Select(p => p.Id)).IsEquivalentTo(packages);
        }
    }
}
