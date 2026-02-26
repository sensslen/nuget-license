// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.IO;
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
    [TestFixture]
    internal class ReferencedPackageReaderTest
    {
        [SetUp]
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
            _lockFileLibraries = _fixture.CreateMany<ILockFileLibrary>(50).ToArray();
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

                    ILockFileLibrary[] referencedLibraries = _lockFileLibraries.Shuffle(rnd)
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

            _uut = new ReferencedPackageReader(_msBuild, _lockFileFactory, _packagesConfigReader);
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
        private IEnumerable<ILockFileLibrary> _lockFileLibraries = null!;
        private IEnumerable<ITargetFrameworkInformation> _packageSpecTargetFrameworks = null!;
        private IEnumerable<INuGetFramework> _targetFrameworks = null!;
        private IFixture _fixture = null!;
        private IDictionary<INuGetFramework, PackageIdentity[]> _referencedPackagesForFramework = null!;
        private IDictionary<INuGetFramework, PackageIdentity[]> _directlyReferencedPackagesForFramework = null!;

        [Test]
        public void GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_AssetsFileContainsErrors(
            [Values] bool includeTransitive)
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

            Assert.That(exception!.Message, Is.EqualTo($"Project assets file for project {_projectPath} contains errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}"));
        }

        [Test]
        public void GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_PackageSpecificationIsInvalid(
            [Values] bool includeTransitive)
        {
            _packageSpecMock.IsValid().Returns(false);
            _projectMock.FullPath.Returns(_projectPath);

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, includeTransitive));

            Assert.That(exception!.Message, Is.EqualTo($"Failed to validate project assets for project {_projectPath}"));
        }

        [Test]
        public void GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_TargetsArrayIsNull(
            [Values] bool includeTransitive)
        {
            _lockFileMock.Targets.Returns((IEnumerable<ILockFileTarget>?)null);

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, includeTransitive));

            Assert.That(exception!.Message, Is.EqualTo($"Failed to validate project assets for project {_projectPath}"));
        }

        [Test]
        public void
            GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_TargetsArrayDoesNotContainAnyElement(
                [Values] bool includeTransitive)
        {
            _lockFileMock.Targets.Returns(Enumerable.Empty<ILockFileTarget>());

            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, includeTransitive));

            Assert.That(exception!.Message, Is.EqualTo($"Failed to validate project assets for project {_projectPath}"));
        }

        [Test]
        public void
            GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_NotIncludingTransitive_And_PackageSpecFrameworkInformationGetFails()
        {
            _packageSpecMock.TargetFrameworks
                .Returns(Enumerable.Empty<ITargetFrameworkInformation>());
            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, false));

            Assert.That(exception!.Message, Is.EqualTo($"Failed to identify the target framework information for {_lockFileTargets.First()}"));
            Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(exception!.InnerException!.Message, Is.EqualTo("Sequence contains no matching element"));
        }

        [Test]
        public void
            GetInstalledPackages_Should_ThrowReferencedPackageReaderException_If_Requested_FrameworkIsNotFound()
        {
            string targetFramework = _fixture.Create<string>();
            _packageSpecMock.TargetFrameworks
                .Returns(Enumerable.Empty<ITargetFrameworkInformation>());
            ReferencedPackageReaderException? exception = Assert.Throws<ReferencedPackageReaderException>(() =>
                _uut.GetInstalledPackages(_projectPath, false, targetFramework));

            Assert.That(exception!.Message, Is.EqualTo($"Target framework {targetFramework} not found."));
        }

        [Test]
        public void
            GetInstalledPackages_Should_ReturnCorrectValues_If_TargetFrameworks_Returns_Empty_And_Requested_Transitive_Packages()
        {
            _packageSpecMock.TargetFrameworks.Returns(Enumerable.Empty<ITargetFrameworkInformation>());
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true);
            Assert.That(result, Is.EquivalentTo(_referencedPackagesForFramework.SelectMany(kvp => kvp.Value).Distinct()));
        }

        [Test]
        public void GetInstalledPackages_Should_GetProjectFromPath([Values] bool includeTransitive)
        {
            _uut.GetInstalledPackages(_projectPath, includeTransitive);
            _msBuild.Received(1).GetProject(Arg.Any<string>());
            _msBuild.Received(1).GetProject(_projectPath);
        }

        [Test]
        public void GetInstalledPackages_Should_TryLoadAssetsFileFromProject([Values] bool includeTransitive)
        {
            _uut.GetInstalledPackages(_projectPath, includeTransitive);
            _projectMock.Received(1).TryGetAssetsPath(out Arg.Any<string>());
            _lockFileFactory.Received(1).GetFromFile(Arg.Any<string>());
            _lockFileFactory.Received(1).GetFromFile(_assetsFilePath);
        }

        [Test]
        public void GetInstalledPackages_Should_ReturnCorrectValues_If_IncludingTransitive()
        {
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true);
            Assert.That(result, Is.EquivalentTo(_referencedPackagesForFramework.SelectMany(kvp => kvp.Value).Distinct()));
        }

        [Test]
        public void GetInstalledPackages_Should_OnlyReturnPackages_For_TargetFramework()
        {
            string name = _fixture.Create<string>();
            INuGetFramework targetFramework = _targetFrameworks.Shuffle(new Random(69843456)).First();
            targetFramework.Equals(name).Returns(true);
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, name);
            Assert.That(result, Is.EquivalentTo(_referencedPackagesForFramework[targetFramework]));
        }

        [Test]
        public void GetInstalledPackages_Should_ReturnCorrectValues_If_NotIncludingTransitive()
        {
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, false);

            PackageIdentity[] expectedReferences = _directlyReferencedPackagesForFramework.SelectMany(p => p.Value)
                .Distinct()
                .ToArray();
            ILockFileLibrary[] expectedResult = _lockFileLibraries.Where(l =>
                    Array.Exists(expectedReferences, e => e.Id.Equals(l.Name)) &&
                    Array.Exists(expectedReferences, e => e.Version!.Equals(l.Version)))
                .ToArray();
            Assert.That(result, Is.EquivalentTo(expectedResult.Select(l => new PackageIdentity(l.Name, l.Version))));
        }

        [Test]
        public void
            GetInstalledPackages_Should_ReturnEmptyCollection_If_Cannot_Get_Asset_File_Path_And_Has_No_Packages_Config()
        {
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(false);
            _projectMock.GetEvaluatedIncludes().Returns(Enumerable.Empty<string>());
            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, false);

            Assert.That(result.Count(), Is.EqualTo(0));
        }

        [Test]
        public void GetInstalledPackages_Should_Use_PackageGonfigReader_If_ProjectIsPackageConfigProject(
            [Values] bool includeTransitive)
        {
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(false);
            _projectMock.FullPath.Returns(_projectPath);
            _projectMock.GetEvaluatedIncludes().Returns(new List<string> { "packages.config" });

            _ = _uut.GetInstalledPackages(_projectPath, includeTransitive);

            _packagesConfigReader.Received(1).GetPackages(Arg.Any<IProject>());
            _packagesConfigReader.Received(1).GetPackages(_projectMock);
        }

        [Test]
        public void GetInstalledPackages_Should_ReturnPackagesReturnedBy_PackageGonfigReader_If_ProjectIsPackageConfigProject(
            [Values] bool includeTransitive)
        {
            _projectMock.TryGetAssetsPath(out Arg.Any<string>()).Returns(false);
            _projectMock.FullPath.Returns(_projectPath);
            _projectMock.GetEvaluatedIncludes().Returns(new List<string> { "packages.config" });
            PackageIdentity[] expectedPackages = _referencedPackagesForFramework.First().Value;
            _packagesConfigReader.GetPackages(Arg.Any<IProject>()).Returns(expectedPackages);

            IEnumerable<PackageIdentity> packages = _uut.GetInstalledPackages(_projectPath, includeTransitive);

            Assert.That(packages, Is.EquivalentTo(expectedPackages));
        }

        [Test]
        public void GetInstalledPackages_Should_ExcludePackages_With_PublishFalse_Metadata()
        {
            string excludedPackage = _fixture.Create<string>();
            string includedPackage = _fixture.Create<string>();

            ILockFileLibrary excludedLibrary = Substitute.For<ILockFileLibrary>();
            excludedLibrary.Name.Returns(excludedPackage);
            excludedLibrary.Version.Returns(Substitute.For<INuGetVersion>());

            ILockFileLibrary includedLibrary = Substitute.For<ILockFileLibrary>();
            includedLibrary.Name.Returns(includedPackage);
            includedLibrary.Version.Returns(Substitute.For<INuGetVersion>());

            ILockFileTarget target = Substitute.For<ILockFileTarget>();
            target.Libraries.Returns(new[] { excludedLibrary, includedLibrary });
            target.TargetFramework.Returns(_targetFrameworks.First());
            _lockFileMock.Targets.Returns(new[] { target });

            _projectMock.GetPackageReferences().Returns(new[]
            {
                new PackageReferenceMetadata(excludedPackage, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Publish"] = "false"
                }),
                new PackageReferenceMetadata(includedPackage, new Dictionary<string, string>())
            });

            IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

            Assert.That(result.Select(p => p.Id), Does.Not.Contain(excludedPackage));
            Assert.That(result.Select(p => p.Id), Does.Contain(includedPackage));
        }

        [Test]
        public void GetInstalledPackages_Should_Keep_SharedTransitiveDependency_If_ReachableFrom_PublishableRoot()
        {
            string tempAssetsPath = Path.GetTempFileName();
            try
            {
                _assetsFilePath = tempAssetsPath;
                _lockFileFactory.GetFromFile(tempAssetsPath).Returns(_lockFileMock);
                File.WriteAllText(tempAssetsPath,
                    """
                    {
                      "targets": {
                        "net10.0": {
                          "PackageA/1.0.0": {
                            "type": "package",
                            "dependencies": {
                              "PackageC": "1.0.0"
                            }
                          },
                          "PackageB/1.0.0": {
                            "type": "package",
                            "dependencies": {
                              "PackageC": "1.0.0"
                            }
                          },
                          "PackageC/1.0.0": {
                            "type": "package"
                          }
                        }
                      }
                    }
                    """);

                INuGetFramework targetFramework = Substitute.For<INuGetFramework>();
                targetFramework.ToString().Returns("net10.0");

                ILockFileTarget target = Substitute.For<ILockFileTarget>();
                target.TargetFramework.Returns(targetFramework);
                ILockFileLibrary[] targetLibraries =
                {
                    CreateLibrary("PackageA"),
                    CreateLibrary("PackageB"),
                    CreateLibrary("PackageC")
                };
                target.Libraries.Returns(targetLibraries);
                _lockFileMock.Targets.Returns(new[] { target });

                ITargetFrameworkInformation targetFrameworkInformation = Substitute.For<ITargetFrameworkInformation>();
                targetFrameworkInformation.FrameworkName.Returns(targetFramework);
                ILibraryDependency[] directDependencies =
                {
                    CreateDependency("PackageA"),
                    CreateDependency("PackageB")
                };
                targetFrameworkInformation.Dependencies.Returns(directDependencies);
                _packageSpecMock.TargetFrameworks.Returns(new[] { targetFrameworkInformation });

                _projectMock.GetPackageReferences().Returns(new[]
                {
                    new PackageReferenceMetadata("PackageA", new Dictionary<string, string>()),
                    new PackageReferenceMetadata("PackageB", new Dictionary<string, string>
                    {
                        ["Publish"] = "false"
                    })
                });

                IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

                Assert.That(result.Select(p => p.Id), Does.Contain("PackageA"));
                Assert.That(result.Select(p => p.Id), Does.Not.Contain("PackageB"));
                Assert.That(result.Select(p => p.Id), Does.Contain("PackageC"));
            }
            finally
            {
                if (File.Exists(tempAssetsPath))
                {
                    File.Delete(tempAssetsPath);
                }
            }
        }

        [Test]
        public void GetInstalledPackages_Should_Exclude_TransitiveDependency_If_OnlyReachableFrom_PublishFalseRoot()
        {
            string tempAssetsPath = Path.GetTempFileName();
            try
            {
                _assetsFilePath = tempAssetsPath;
                _lockFileFactory.GetFromFile(tempAssetsPath).Returns(_lockFileMock);
                File.WriteAllText(tempAssetsPath,
                    """
                    {
                      "targets": {
                        "net10.0": {
                          "PackageA/1.0.0": {
                            "type": "package"
                          },
                          "PackageB/1.0.0": {
                            "type": "package",
                            "dependencies": {
                              "PackageC": "1.0.0"
                            }
                          },
                          "PackageC/1.0.0": {
                            "type": "package"
                          }
                        }
                      }
                    }
                    """);

                INuGetFramework targetFramework = Substitute.For<INuGetFramework>();
                targetFramework.ToString().Returns("net10.0");

                ILockFileTarget target = Substitute.For<ILockFileTarget>();
                target.TargetFramework.Returns(targetFramework);
                ILockFileLibrary[] targetLibraries =
                {
                    CreateLibrary("PackageA"),
                    CreateLibrary("PackageB"),
                    CreateLibrary("PackageC")
                };
                target.Libraries.Returns(targetLibraries);
                _lockFileMock.Targets.Returns(new[] { target });

                ITargetFrameworkInformation targetFrameworkInformation = Substitute.For<ITargetFrameworkInformation>();
                targetFrameworkInformation.FrameworkName.Returns(targetFramework);
                ILibraryDependency[] directDependencies =
                {
                    CreateDependency("PackageA"),
                    CreateDependency("PackageB")
                };
                targetFrameworkInformation.Dependencies.Returns(directDependencies);
                _packageSpecMock.TargetFrameworks.Returns(new[] { targetFrameworkInformation });

                _projectMock.GetPackageReferences().Returns(new[]
                {
                    new PackageReferenceMetadata("PackageA", new Dictionary<string, string>()),
                    new PackageReferenceMetadata("PackageB", new Dictionary<string, string>
                    {
                        ["Publish"] = "false"
                    })
                });

                IEnumerable<PackageIdentity> result = _uut.GetInstalledPackages(_projectPath, true, null, true);

                Assert.That(result.Select(p => p.Id), Does.Contain("PackageA"));
                Assert.That(result.Select(p => p.Id), Does.Not.Contain("PackageB"));
                Assert.That(result.Select(p => p.Id), Does.Not.Contain("PackageC"));
            }
            finally
            {
                if (File.Exists(tempAssetsPath))
                {
                    File.Delete(tempAssetsPath);
                }
            }
        }

        private static ILibraryDependency CreateDependency(string packageName)
        {
            ILibraryDependency dependency = Substitute.For<ILibraryDependency>();
            dependency.Name.Returns(packageName);
            return dependency;
        }

        private static ILockFileLibrary CreateLibrary(string packageName)
        {
            ILockFileLibrary library = Substitute.For<ILockFileLibrary>();
            library.Name.Returns(packageName);
            library.Type.Returns("package");
            library.Version.Returns(Substitute.For<INuGetVersion>());
            return library;
        }
    }
}
