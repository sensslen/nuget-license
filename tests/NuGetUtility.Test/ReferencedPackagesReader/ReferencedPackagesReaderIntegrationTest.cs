// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;
using TUnit.Core.Enums;

namespace NuGetUtility.Test.ReferencedPackagesReader
{
    public class ReferencedPackagesReaderIntegrationTest
    {
        [Before(Test)]
        public void SetUp()
        {
            IPackagesConfigReader packagesConfigReader = OperatingSystem.IsWindows() ? new WindowsPackagesConfigReader() : new FailingPackagesConfigReader();

            _uut = new ReferencedPackageReader(
                new MsBuildAbstraction(),
                new LockFileFactory(),
                new NuGetFrameworkUtility(),
                new AssetsPackageDependencyReader(new NuGetFrameworkUtility()),
                packagesConfigReader);
        }

        private ReferencedPackageReader? _uut;

        [Test]
        public async Task GetInstalledPackagesShould_ReturnPackagesForActualProjectCorrectly()
        {
            string path = Path.GetFullPath("../../../../targets/PackageReferenceProject/PackageReferenceProject.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, false);

            await Assert.That(result.Count()).IsEqualTo(1);
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnTransitivePackages()
        {
            string path = Path.GetFullPath(
                "../../../../targets/ProjectWithTransitiveReferences/ProjectWithTransitiveReferences.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, true);

            await Assert.That(result.Count()).IsEqualTo(1);
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnTransitiveNuGet()
        {
            string path = Path.GetFullPath(
                "../../../../targets/ProjectWithTransitiveNuget/ProjectWithTransitiveNuget.csproj");

            PackageIdentity[] result = _uut!.GetInstalledPackages(path, true).ToArray();

            await Assert.That(result.Count()).IsEqualTo(3);
            string[] titles = result.Select(metadata => metadata.Id).ToArray();
            await Assert.That(titles.Contains("NSubstitute")).IsTrue();
            await Assert.That(titles.Contains("Castle.Core")).IsTrue();
            await Assert.That(titles.Contains("System.Diagnostics.EventLog")).IsTrue();
        }

        [Test]
        public async Task GetInstalledPackagesShould_ReturnEmptyEnumerable_For_ProjectsWithoutPackages()
        {
            string path = Path.GetFullPath(
                "../../../../targets/ProjectWithoutNugetReferences/ProjectWithoutNugetReferences.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, false);

            await Assert.That(result.Count()).IsEqualTo(0);
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackagesShould_ReturnResolvedDependency_For_ProjectWithRangedDependencies(bool includeTransitive)
        {
            string path = Path.GetFullPath(
                "../../../../targets/VersionRangesProject/VersionRangesProject.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, includeTransitive);

            await Assert.That(result.Count()).IsEqualTo(includeTransitive ? 3 : 1);
        }

        [RunOn(OS.Windows)]
        [Test]
        public async Task GetInstalledPackagesShould_ReturnPackages_For_PackagesConfigProject()
        {
            string path = Path.GetFullPath("../../../../targets/PackagesConfigProject/PackagesConfigProject.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, false);

            await Assert.That(result.Count()).IsEqualTo(1);
        }

        [ExcludeOn(OS.Windows)]
        [Test]
        public async Task GetInstalledPackagesShould_ThrowError_PackagesConfigProject()
        {
            string path = Path.GetFullPath("../../../../targets/PackagesConfigProject/PackagesConfigProject.csproj");

            PackagesConfigReaderException exception = Assert.Throws<PackagesConfigReaderException>(() => _uut!.GetInstalledPackages(path, false));
            await Assert.That(exception.Message).IsEqualTo($"Invalid project structure detected. Currently packages.config projects are only supported on Windows (Project: {path})");
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackagesShould_ThrowError_For_PackagesForNativeCppProject_With_References(bool includeTransitive)
        {
            string path = Path.GetFullPath("../../../../targets/SimpleCppProject/SimpleCppProject.vcxproj");

            MsBuildAbstractionException exception = Assert.Throws<MsBuildAbstractionException>(() => _uut!.GetInstalledPackages(path, includeTransitive));
            await Assert.That(exception.Message).IsEqualTo($"Please use the .net Framework version to analyze c++ projects (Project: {path})");
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackagesShould_ThrowError_For_PackagesForNativeCppProject_Without_References(bool includeTransitive)
        {
            string path = Path.GetFullPath("../../../../targets/EmptyCppProject/EmptyCppProject.vcxproj");

            MsBuildAbstractionException exception = Assert.Throws<MsBuildAbstractionException>(() => _uut!.GetInstalledPackages(path, includeTransitive));
            await Assert.That(exception.Message).IsEqualTo($"Please use the .net Framework version to analyze c++ projects (Project: {path})");
        }

        [Arguments("net9.0", false, "TinyCsvParser")]
        [Arguments("net8.0", false, "Microsoft.Extensions.Logging.Abstractions")]
        [Arguments("net8.0-browser", false, "Microsoft.Extensions.Logging.Abstractions")]
        [Arguments("net9.0", true, "TinyCsvParser")]
        [Arguments("net8.0", true, "Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.DependencyInjection.Abstractions", "System.Diagnostics.DiagnosticSource")]
        [Arguments("net8.0-browser", true, "Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.DependencyInjection.Abstractions", "System.Diagnostics.DiagnosticSource")]
        [Test]
        public async Task GetInstalledPackagesShould_OnlyReturn_PackagesPackagesReferencedByRequestedFramework(string framework, bool includeTransitive, params string[] packages)
        {
            string path = Path.GetFullPath("../../../../targets/MultiTargetProjectWithDifferentDependencies/MultiTargetProjectWithDifferentDependencies.csproj");

            IEnumerable<PackageIdentity> result = _uut!.GetInstalledPackages(path, includeTransitive, framework);

            await Assert.That(result.Select(p => p.Id)).IsEquivalentTo(packages, CollectionOrdering.Any);
        }
    }
}
