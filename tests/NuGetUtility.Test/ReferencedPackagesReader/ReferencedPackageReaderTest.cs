// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using AutoFixture.AutoNSubstitute;
using NSubstitute;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.MsBuildWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Test.ReferencedPackagesReader
{
    internal class ReferencedPackageReaderTest
    {
        [Before(Test)]
        public void SetUp()
        {
            _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
            _msBuild = Substitute.For<IMsBuildAbstraction>();
            _lockFileFactory = Substitute.For<ILockFileFactory>();
            _projectPath = _fixture.Create<string>();
            _assetsFilePath = _fixture.Create<string>();
            _projectMock = Substitute.For<IProject>();
            _lockFileMock = Substitute.For<ILockFile>();
            _packageSpecMock = Substitute.For<IPackageSpec>();
            _packagesConfigReader = Substitute.For<IPackagesConfigReader>();
            _lockFileTargets = _fixture.CreateMany<ILockFileTarget>(TargetFrameworkCount).ToArray();
            _lockFileLibraries = _fixture.CreateMany<ILockFileTargetLibrary>(50).ToArray();
            _packageSpecTargetFrameworks =
                _fixture.CreateMany<ITargetFrameworkInformation>(TargetFrameworkCount).ToArray();
            _targetFrameworks = _fixture.CreateMany<INuGetFramework>(TargetFrameworkCount).ToArray();
            _referencedPackagesForFramework = new Dictionary<INuGetFramework, PackageIdentity[]>();
            _directlyReferencedPackagesForFramework = new Dictionary<INuGetFramework, PackageIdentity[]>();

            _msBuild.GetProject(_projectPath).Returns(_projectMock);
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(args =>
            {
                args[0] = _assetsFilePath;
                return true;
            });
            _projectMock.FullPath.Returns(_projectPath);
            _projectMock.GetPackageReferences().Returns(Array.Empty<PackageReferenceMetadata>());
            _projectMock.GetPackageReferencesForTarget(Arg.Any<string>()).Returns(Array.Empty<PackageReferenceMetadata>());
            _lockFileFactory.GetFromFile(_assetsFilePath).Returns(_lockFileMock);
            _lockFileMock.PackageSpec.Returns(_packageSpecMock);
            _packageSpecMock.IsValid().Returns(true);
            _lockFileMock.Targets.Returns(_lockFileTargets);
            _packageSpecMock.TargetFrameworks.Returns(_packageSpecTargetFrameworks);

            var rnd = new Random(75643);
            foreach (ILockFileLibrary lockFileLibrary in _lockFileLibraries)
            {
                INuGetVersion version = Substitute.For<INuGetVersion>();
                lockFileLibrary.Version.Returns(version);
                lockFileLibrary.Name.Returns(_fixture.Create<string>());
            }

            foreach (INuGetFramework targetFramework in _targetFrameworks)
            {
                targetFramework.ToString().Returns(_fixture.Create<string>());
            }

            using (IEnumerator<INuGetFramework> targetFrameworksIterator = _targetFrameworks.GetEnumerator())
            {
                foreach (ILockFileTarget lockFileTarget in _lockFileTargets)
                {
                    targetFrameworksIterator.MoveNext();
                    lockFileTarget.TargetFramework.Returns(targetFrameworksIterator.Current);

                    ILockFileTargetLibrary[] referencedLibraries = _lockFileLibraries.Shuffle(rnd)
                        .Take(5)
                        .ToArray();
                    _referencedPackagesForFramework[targetFrameworksIterator.Current] = referencedLibraries.Select(l => new PackageIdentity(l.Name, l.Version!)).ToArray();
                    lockFileTarget.Libraries.Returns(referencedLibraries);
                }
            }

            using (IEnumerator<INuGetFramework> targetFrameworksIterator = _targetFrameworks.GetEnumerator())
            {
                foreach (ITargetFrameworkInformation packageSpecTargetFramework in _packageSpecTargetFrameworks)
                {
                    targetFrameworksIterator.MoveNext();
                    packageSpecTargetFramework.FrameworkName
                        .Returns(targetFrameworksIterator.Current);

                    PackageIdentity[] directDependencies = _referencedPackagesForFramework[targetFrameworksIterator.Current].Shuffle(rnd)
                        .Take(2).ToArray();

                    _directlyReferencedPackagesForFramework[targetFrameworksIterator.Current] = directDependencies;
                    packageSpecTargetFramework.Dependencies.Returns(directDependencies.Select(l =>
                    {
                        ILibraryDependency sub = Substitute.For<ILibraryDependency>();
                        sub.Name.Returns(l.Id);
                        return sub;
                    }));
                }
            }

            _uut = new ReferencedPackageReader(
                _msBuild,
                _lockFileFactory,
                new NuGetFrameworkUtility(),
                new AssetsPackageDependencyReader(new NuGetFrameworkUtility()),
                _packagesConfigReader);
        }

        private const int TargetFrameworkCount = 5;
        private ReferencedPackageReader _uut = null!;
        private IMsBuildAbstraction _msBuild = null!;
        private ILockFileFactory _lockFileFactory = null!;
        private IPackagesConfigReader _packagesConfigReader = null!;
        private string _projectPath = null!;
        private string _assetsFilePath = null!;
        private IProject _projectMock = null!;
        private ILockFile _lockFileMock = null!;
        private IPackageSpec _packageSpecMock = null!;
        private IEnumerable<ILockFileTarget> _lockFileTargets = null!;
        private IEnumerable<ILockFileTargetLibrary> _lockFileLibraries = null!;
        private IEnumerable<ITargetFrameworkInformation> _packageSpecTargetFrameworks = null!;
        private IEnumerable<INuGetFramework> _targetFrameworks = null!;
        private IFixture _fixture = null!;
        private IDictionary<INuGetFramework, PackageIdentity[]> _referencedPackagesForFramework = null!;
        private IDictionary<INuGetFramework, PackageIdentity[]> _directlyReferencedPackagesForFramework = null!;

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_AssetsFileContainsErrors(
            bool includeTransitive)
        {
            string[] errors = _fixture.CreateMany<string>().ToArray();
            _lockFileMock.TryGetErrors(out Arg.Any<string[]>()).Returns(args =>
            {
                args[0] = errors;
                return true;
            });
            _projectMock.FullPath.Returns(_projectPath);

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, includeTransitive));

            await Assert.That(exception!.Message).IsEqualTo($"Project assets file for project {_projectPath} contains errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_PackageSpecificationIsInvalid(
            bool includeTransitive)
        {
            _packageSpecMock.IsValid().Returns(false);
            _projectMock.FullPath.Returns(_projectPath);

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, includeTransitive));

            await Assert.That(exception!.Message).IsEqualTo($"Failed to validate project assets for project {_projectPath}");
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task
            GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_TargetsArrayDoesNotContainAnyElement(
                bool includeTransitive)
        {
            _lockFileMock.Targets.Returns(Enumerable.Empty<ILockFileTarget>());

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, includeTransitive));

            await Assert.That(exception!.Message).IsEqualTo($"Failed to validate project assets for project {_projectPath}");
        }

        [Test]
        public async Task
            GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_NotIncludingTransitive_And_PackageSpecFrameworkInformationGetFails()
        {
            _packageSpecMock.TargetFrameworks
                .Returns(Enumerable.Empty<ITargetFrameworkInformation>());
            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, false));

            await Assert.That(exception!.Message).IsEqualTo($"Failed to identify the target framework information for {_lockFileTargets.First()}");
            await Assert.That(exception.InnerException).IsAssignableTo<InvalidOperationException>();
            await Assert.That(exception!.InnerException!.Message).IsEqualTo("Sequence contains no matching element");
        }

        [Test]
        public async Task
            GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_Requested_FrameworkIsNotFound()
        {
            const string targetFramework = "net10.0";

            ILockFileTarget targetNet80 = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkNet80 = Substitute.For<INuGetFramework>();
            frameworkNet80.ToString().Returns("net8.0");
            targetNet80.TargetFramework.Returns(frameworkNet80);
            targetNet80.Libraries.Returns(Array.Empty<ILockFileTargetLibrary>());

            ILockFileTarget targetNet90 = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkNet90 = Substitute.For<INuGetFramework>();
            frameworkNet90.ToString().Returns("net9.0");
            targetNet90.TargetFramework.Returns(frameworkNet90);
            targetNet90.Libraries.Returns(Array.Empty<ILockFileTargetLibrary>());

            _lockFileMock.Targets.Returns([targetNet80, targetNet90]);

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, false, targetFramework));

            await Assert.That(exception!.Message).IsEqualTo($"Target framework {targetFramework} not found.");
        }

        [Test]
        public async Task
            GetInstalledPackages_Should_ReturnCorrectValues_If_TargetFrameworks_Returns_Empty_And_Requested_Transitive_Packages()
        {
            _packageSpecMock.TargetFrameworks.Returns(Enumerable.Empty<ITargetFrameworkInformation>());
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true);
            await Assert.That(result).IsEquivalentTo(_referencedPackagesForFramework.SelectMany(kvp => kvp.Value).Distinct(), CollectionOrdering.Any);
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackages_Should_GetProjectFromPath(bool includeTransitive)
        {
            _uut.GetInstalledPackages(_projectPath, includeTransitive);
            _msBuild.Received(1).GetProject(Arg.Any<string>());
            _msBuild.Received(1).GetProject(_projectPath);
            await Task.CompletedTask;
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackages_Should_TryLoadAssetsFileFromProject(bool includeTransitive)
        {
            _uut.GetInstalledPackages(_projectPath, includeTransitive);
            _projectMock.Received(1).TryGetAssetsPath(out Arg.Any<string>());
            _lockFileFactory.Received(1).GetFromFile(Arg.Any<string>());
            _lockFileFactory.Received(1).GetFromFile(_assetsFilePath);
            await Task.CompletedTask;
        }

        [Test]
        public async Task GetInstalledPackages_Should_ReturnCorrectValues_If_IncludingTransitive()
        {
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true);
            await Assert.That(result).IsEquivalentTo(_referencedPackagesForFramework.SelectMany(kvp => kvp.Value).Distinct(), CollectionOrdering.Any);
        }

        [Test]
        public async Task GetInstalledPackages_Should_OnlyReturnPackages_For_TargetFramework()
        {
            const string requestedTargetFramework = "net8.0";

            ILockFileTargetLibrary net80Library = CreateLibrary("PackageNet80");
            ILockFileTargetLibrary net90Library = CreateLibrary("PackageNet90");

            ILockFileTarget targetNet80 = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkNet80 = Substitute.For<INuGetFramework>();
            frameworkNet80.ToString().Returns("net8.0");
            targetNet80.TargetFramework.Returns(frameworkNet80);
            targetNet80.Libraries.Returns([net80Library]);

            ILockFileTarget targetNet90 = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkNet90 = Substitute.For<INuGetFramework>();
            frameworkNet90.ToString().Returns("net9.0");
            targetNet90.TargetFramework.Returns(frameworkNet90);
            targetNet90.Libraries.Returns([net90Library]);

            _lockFileMock.Targets.Returns([targetNet80, targetNet90]);

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, requestedTargetFramework);

            await Assert.That(result.Select(package => package.Id)).IsEquivalentTo(["PackageNet80"], CollectionOrdering.Any);
            await Assert.That(result.Select(package => package.Id)).DoesNotContain("PackageNet90");
        }

        [Test]
        public async Task GetInstalledPackages_Should_OnlyReturnPackages_For_Equivalent_TargetFramework_Representation()
        {
            const string requestedTargetFramework = "net8.0";

            ILockFileTargetLibrary equivalentTargetLibrary = CreateLibrary("PackageEquivalent");
            ILockFileTargetLibrary otherTargetLibrary = CreateLibrary("PackageOther");

            ILockFileTarget targetEquivalent = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkEquivalent = Substitute.For<INuGetFramework>();
            frameworkEquivalent.ToString().Returns(".NETCoreApp,Version=v8.0");
            targetEquivalent.TargetFramework.Returns(frameworkEquivalent);
            targetEquivalent.Libraries.Returns([equivalentTargetLibrary]);

            ILockFileTarget targetOther = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkOther = Substitute.For<INuGetFramework>();
            frameworkOther.ToString().Returns("net9.0");
            targetOther.TargetFramework.Returns(frameworkOther);
            targetOther.Libraries.Returns([otherTargetLibrary]);

            _lockFileMock.Targets.Returns([targetEquivalent, targetOther]);

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, requestedTargetFramework);

            await Assert.That(result.Select(package => package.Id)).IsEquivalentTo(["PackageEquivalent"], CollectionOrdering.Any);
            await Assert.That(result.Select(package => package.Id)).DoesNotContain("PackageOther");
        }

        [Arguments("net8.0")]
        [Arguments("NET8.0")]
        [Arguments(" .NETCoreApp,Version=v8.0 ")]
        [Arguments(".NETCoreApp,Version=v8.0")]
        [Test]
        public async Task GetInstalledPackages_Should_OnlyReturnPackages_For_TargetFramework_Variants(string requestedTargetFramework)
        {
            ILockFileTargetLibrary variantTargetLibrary = CreateLibrary("PackageVariant");
            ILockFileTargetLibrary otherTargetLibrary = CreateLibrary("PackageOther");

            ILockFileTarget targetVariant = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkVariant = Substitute.For<INuGetFramework>();
            frameworkVariant.ToString().Returns("net8.0");
            targetVariant.TargetFramework.Returns(frameworkVariant);
            targetVariant.Libraries.Returns([variantTargetLibrary]);

            ILockFileTarget targetOther = Substitute.For<ILockFileTarget>();
            INuGetFramework frameworkOther = Substitute.For<INuGetFramework>();
            frameworkOther.ToString().Returns("net9.0");
            targetOther.TargetFramework.Returns(frameworkOther);
            targetOther.Libraries.Returns([otherTargetLibrary]);

            _lockFileMock.Targets.Returns([targetVariant, targetOther]);

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, requestedTargetFramework);

            await Assert.That(result.Select(package => package.Id)).IsEquivalentTo(["PackageVariant"], CollectionOrdering.Any);
            await Assert.That(result.Select(package => package.Id)).DoesNotContain("PackageOther");
        }

        [Test]
        public async Task GetInstalledPackages_Should_ReturnCorrectValues_If_NotIncludingTransitive()
        {
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, false);

            PackageIdentity[] expectedReferences = _directlyReferencedPackagesForFramework.SelectMany(p => p.Value)
                .Distinct()
                .ToArray();
            ILockFileLibrary[] expectedResult = _lockFileLibraries.Where(l =>
                    Array.Exists(expectedReferences, e => e.Id.Equals(l.Name)) &&
                    Array.Exists(expectedReferences, e => e.Version!.Equals(l.Version)))
                .ToArray();
            await Assert.That(result).IsEquivalentTo(expectedResult.Select(l => new PackageIdentity(l.Name, l.Version)), CollectionOrdering.Any);
        }

        [Test]
        public async Task
            GetInstalledPackages_Should_ReturnEmptyCollection_If_Cannot_Get_Asset_File_Path_And_Has_No_Packages_Config()
        {
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(false);
            _projectMock.GetEvaluatedIncludes().Returns(Enumerable.Empty<string>());
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, false);

            await Assert.That(result.Count()).IsEqualTo(0);
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackages_Should_Use_PackageGonfigReader_If_ProjectIsPackageConfigProject(
            bool includeTransitive)
        {
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(false);
            _projectMock.FullPath.Returns(_projectPath);
            _projectMock.GetEvaluatedIncludes().Returns(new List<string> { "packages.config" });

            _ = _uut.GetInstalledPackages(_projectPath, includeTransitive);

            _packagesConfigReader.Received(1).GetPackages(Arg.Any<IProject>());
            _packagesConfigReader.Received(1).GetPackages(_projectMock);
            await Task.CompletedTask;
        }

        [Arguments(true)]
        [Arguments(false)]
        [Test]
        public async Task GetInstalledPackages_Should_ReturnPackagesReturnedBy_PackageGonfigReader_If_ProjectIsPackageConfigProject(
            bool includeTransitive)
        {
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(false);
            _projectMock.FullPath.Returns(_projectPath);
            _projectMock.GetEvaluatedIncludes().Returns(new List<string> { "packages.config" });
            PackageIdentity[] expectedPackages = _referencedPackagesForFramework.First().Value;
            _packagesConfigReader.GetPackages(Arg.Any<IProject>()).Returns(expectedPackages);

            IEnumerable<PackageIdentity> packages = _uut.GetInstalledPackages(_projectPath, includeTransitive);

            await Assert.That(packages).IsEquivalentTo(expectedPackages, CollectionOrdering.Any);
        }

        [Test]
        public async Task GetInstalledPackages_Should_ExcludePackages_With_PublishFalse_Metadata()
        {
            string excludedPackage = _fixture.Create<string>();
            string includedPackage = _fixture.Create<string>();

            ILockFileTargetLibrary excludedLibrary = Substitute.For<ILockFileTargetLibrary>();
            excludedLibrary.Name.Returns(excludedPackage);
            excludedLibrary.Version.Returns(Substitute.For<INuGetVersion>());

            ILockFileTargetLibrary includedLibrary = Substitute.For<ILockFileTargetLibrary>();
            includedLibrary.Name.Returns(includedPackage);
            includedLibrary.Version.Returns(Substitute.For<INuGetVersion>());

            ILockFileTarget target = Substitute.For<ILockFileTarget>();
            target.Libraries.Returns([excludedLibrary, includedLibrary]);
            INuGetFramework targetFramework = Substitute.For<INuGetFramework>();
            targetFramework.ToString().Returns("net8.0");
            target.TargetFramework.Returns(targetFramework);
            _lockFileMock.Targets.Returns([target]);

            _projectMock.GetPackageReferences().Returns(
            [
                new PackageReferenceMetadata(excludedPackage, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Publish"] = "false"
                }),
                new PackageReferenceMetadata(includedPackage, new Dictionary<string, string>())
            ]);
            _projectMock.GetPackageReferencesForTarget("net8.0").Returns(
            [
                new PackageReferenceMetadata(excludedPackage, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Publish"] = "false"
                }),
                new PackageReferenceMetadata(includedPackage, new Dictionary<string, string>())
            ]);

            ITargetFrameworkInformation targetFrameworkInformation = Substitute.For<ITargetFrameworkInformation>();
            targetFrameworkInformation.FrameworkName.Returns(targetFramework);
            ILibraryDependency excludedDependency = CreateDependency(excludedPackage);
            ILibraryDependency includedDependency = CreateDependency(includedPackage);
            targetFrameworkInformation.Dependencies.Returns([excludedDependency, includedDependency]);
            _packageSpecMock.TargetFrameworks.Returns([targetFrameworkInformation]);

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

            await Assert.That(result.Select(p => p.Id)).DoesNotContain(excludedPackage);
            await Assert.That(result.Select(p => p.Id)).Contains(includedPackage);
        }

        [Test]
        public async Task GetInstalledPackages_Should_Apply_PublishFalse_PerTarget_When_TargetFramework_IsNull()
        {
            const string packageName = "PackageConditional";

            INuGetFramework net80 = Substitute.For<INuGetFramework>();
            net80.ToString().Returns("net8.0");

            INuGetFramework net90 = Substitute.For<INuGetFramework>();
            net90.ToString().Returns("net9.0");

            ILockFileTarget targetNet80 = Substitute.For<ILockFileTarget>();
            targetNet80.TargetFramework.Returns(net80);
            ILockFileTargetLibrary net80Library = CreateLibrary(packageName);
            targetNet80.Libraries.Returns([net80Library]);

            ILockFileTarget targetNet90 = Substitute.For<ILockFileTarget>();
            targetNet90.TargetFramework.Returns(net90);
            ILockFileTargetLibrary net90Library = CreateLibrary(packageName);
            targetNet90.Libraries.Returns([net90Library]);

            _lockFileMock.Targets.Returns([targetNet80, targetNet90]);

            ITargetFrameworkInformation net80Info = Substitute.For<ITargetFrameworkInformation>();
            net80Info.FrameworkName.Returns(net80);
            ILibraryDependency net80Dependency = CreateDependency(packageName);
            net80Info.Dependencies.Returns([net80Dependency]);

            ITargetFrameworkInformation net90Info = Substitute.For<ITargetFrameworkInformation>();
            net90Info.FrameworkName.Returns(net90);
            ILibraryDependency net90Dependency = CreateDependency(packageName);
            net90Info.Dependencies.Returns([net90Dependency]);

            _packageSpecMock.TargetFrameworks.Returns([net80Info, net90Info]);

            _projectMock.GetPackageReferences().Returns(
            [
                new PackageReferenceMetadata(packageName, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Publish"] = "false"
                })
            ]);

            _projectMock.GetPackageReferencesForTarget("net8.0").Returns(
            [
                new PackageReferenceMetadata(packageName, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Publish"] = "false"
                })
            ]);

            _projectMock.GetPackageReferencesForTarget("net9.0").Returns(Array.Empty<PackageReferenceMetadata>());

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

            await Assert.That(result.Select(p => p.Id)).Contains(packageName);
            _projectMock.Received(1).GetPackageReferencesForTarget("net8.0");
            _projectMock.Received(1).GetPackageReferencesForTarget("net9.0");
            _projectMock.DidNotReceive().GetPackageReferences();
        }

        [Test]
        public async Task GetInstalledPackages_Should_Keep_SharedTransitiveDependency_If_ReachableFrom_PublishableRoot()
        {
            INuGetFramework targetFramework = Substitute.For<INuGetFramework>();
            targetFramework.ToString().Returns("net10.0");

            ILockFileTarget target = Substitute.For<ILockFileTarget>();
            target.TargetFramework.Returns(targetFramework);
            ILockFileTargetLibrary[] targetLibraries =
            [
                CreateLibrary("PackageA", "PackageC"),
                CreateLibrary("PackageB", "PackageC"),
                CreateLibrary("PackageC")
            ];
            target.Libraries.Returns(targetLibraries);
            _lockFileMock.Targets.Returns([target]);

            ITargetFrameworkInformation targetFrameworkInformation = Substitute.For<ITargetFrameworkInformation>();
            targetFrameworkInformation.FrameworkName.Returns(targetFramework);
            ILibraryDependency[] directDependencies =
            [
                CreateDependency("PackageA"),
                CreateDependency("PackageB")
            ];
            targetFrameworkInformation.Dependencies.Returns(directDependencies);
            _packageSpecMock.TargetFrameworks.Returns([targetFrameworkInformation]);

            _projectMock.GetPackageReferences().Returns(
            [
                new PackageReferenceMetadata("PackageA", new Dictionary<string, string>()),
                    new PackageReferenceMetadata("PackageB", new Dictionary<string, string>
                    {
                        ["Publish"] = "false"
                    })
            ]);
            _projectMock.GetPackageReferencesForTarget("net10.0").Returns(
            [
                new PackageReferenceMetadata("PackageA", new Dictionary<string, string>()),
                    new PackageReferenceMetadata("PackageB", new Dictionary<string, string>
                    {
                        ["Publish"] = "false"
                    })
            ]);

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

            await Assert.That(result.Select(p => p.Id)).Contains("PackageA");
            await Assert.That(result.Select(p => p.Id)).DoesNotContain("PackageB");
            await Assert.That(result.Select(p => p.Id)).Contains("PackageC");
        }

        [Test]
        public async Task GetInstalledPackages_Should_Exclude_TransitiveDependency_If_OnlyReachableFrom_PublishFalseRoot()
        {
            INuGetFramework targetFramework = Substitute.For<INuGetFramework>();
            targetFramework.ToString().Returns("net10.0");

            ILockFileTarget target = Substitute.For<ILockFileTarget>();
            target.TargetFramework.Returns(targetFramework);
            ILockFileTargetLibrary[] targetLibraries =
            [
                CreateLibrary("PackageA"),
                CreateLibrary("PackageB", "PackageC"),
                CreateLibrary("PackageC")
            ];
            target.Libraries.Returns(targetLibraries);
            _lockFileMock.Targets.Returns([target]);

            ITargetFrameworkInformation targetFrameworkInformation = Substitute.For<ITargetFrameworkInformation>();
            targetFrameworkInformation.FrameworkName.Returns(targetFramework);
            ILibraryDependency[] directDependencies =
            [
                CreateDependency("PackageA"),
                CreateDependency("PackageB")
            ];
            targetFrameworkInformation.Dependencies.Returns(directDependencies);
            _packageSpecMock.TargetFrameworks.Returns([targetFrameworkInformation]);

            _projectMock.GetPackageReferences().Returns(
            [
                new PackageReferenceMetadata("PackageA", new Dictionary<string, string>()),
                    new PackageReferenceMetadata("PackageB", new Dictionary<string, string>
                    {
                        ["Publish"] = "false"
                    })
            ]);
            _projectMock.GetPackageReferencesForTarget("net10.0").Returns(
            [
                new PackageReferenceMetadata("PackageA", new Dictionary<string, string>()),
                    new PackageReferenceMetadata("PackageB", new Dictionary<string, string>
                    {
                        ["Publish"] = "false"
                    })
            ]);

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

            await Assert.That(result.Select(p => p.Id)).Contains("PackageA");
            await Assert.That(result.Select(p => p.Id)).DoesNotContain("PackageB");
            await Assert.That(result.Select(p => p.Id)).DoesNotContain("PackageC");
        }

        private static ILibraryDependency CreateDependency(string packageName)
        {
            ILibraryDependency dependency = Substitute.For<ILibraryDependency>();
            dependency.Name.Returns(packageName);
            return dependency;
        }

        private static ILockFileTargetLibrary CreateLibrary(string packageName, params string[] dependencyNames)
        {
            ILockFileTargetLibrary library = Substitute.For<ILockFileTargetLibrary>();
            library.Name.Returns(packageName);
            library.Type.Returns("package");
            library.Version.Returns(Substitute.For<INuGetVersion>());
            IPackageDependency[] dependencies = dependencyNames.Select(CreatePackageDependency).ToArray();
            library.Dependencies.Returns(dependencies);
            return library;
        }

        private static IPackageDependency CreatePackageDependency(string dependencyName)
        {
            IPackageDependency packageDependency = Substitute.For<IPackageDependency>();
            packageDependency.Id.Returns(dependencyName);
            return packageDependency;
        }
    }
}
