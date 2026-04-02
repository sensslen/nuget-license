// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using NSubstitute;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

namespace NuGetUtility.Test.ReferencedPackagesReader
{
    public class ProjectsCollectorTest
    {
        public ProjectsCollectorTest()
        {
            _osPlatformSpecificVerifySettings = new();
            _osPlatformSpecificVerifySettings.UniqueForOSPlatform();
        }

        [Before(Test)]
        public void SetUp()
        {
            _fixture = new Fixture();
            _solutionPersistanceWrapper = Substitute.For<ISolutionPersistanceWrapper>();
            _fileSystem = new MockFileSystem();
            _uut = new ProjectsCollector(_solutionPersistanceWrapper, _fileSystem);
        }
        private ISolutionPersistanceWrapper _solutionPersistanceWrapper = null!;
        private IFileSystem _fileSystem = null!;
        private ProjectsCollector _uut = null!;
        private Fixture _fixture = null!;
        private readonly VerifySettings _osPlatformSpecificVerifySettings;

        [Arguments("A.csproj")]
        [Arguments("B.fsproj")]
        [Arguments("C.vbproj")]
        [Arguments("D.dbproj")]
        [Test]
        public async Task GetProjects_Should_ReturnProjectsAsListDirectly(string projectFile)
        {
            IEnumerable<string> result = await _uut.GetProjectsAsync(projectFile);
            await Assert.That(result).IsEquivalentTo([_fileSystem.Path.GetFullPath(projectFile)], CollectionOrdering.InOrder);
            await _solutionPersistanceWrapper.DidNotReceive().GetProjectsFromSolutionAsync(Arg.Any<string>());
        }

        [Arguments("A.sln")]
        [Arguments("B.sln")]
        [Arguments("C.sln")]
        [Arguments("A.slnx")]
        [Test]
        public async Task GetProjects_Should_QueryMsBuildToGetProjectsForSolutionFiles(string solutionFile)
        {
            _ = await _uut.GetProjectsAsync(solutionFile);

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(_fileSystem.Path.GetFullPath(solutionFile));
        }

        [Arguments("A.sln")]
        [Arguments("B.sln")]
        [Arguments("C.sln")]
        [Arguments("C.slnx")]
        [Test]
        public async Task GetProjects_Should_ReturnEmptyArray_If_SolutionContainsNoProjects(string solutionFile)
        {
            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult(Enumerable.Empty<string>()));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            await Assert.That(result).IsEmpty();

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(_fileSystem.Path.GetFullPath(solutionFile));
        }

        [Arguments("A.sln")]
        [Arguments("B.sln")]
        [Arguments("C.sln")]
        [Arguments("B.slnx")]
        [Test]
        public async Task GetProjects_Should_ReturnEmptyArray_If_SolutionContainsProjectsThatDontExist(string solutionFile)
        {
            IEnumerable<string> projects = _fixture.CreateMany<string>();
            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult(projects));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            await Assert.That(result).IsEmpty();

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(_fileSystem.Path.GetFullPath(solutionFile));
        }

        [Arguments("A.sln")]
        [Arguments("B.sln")]
        [Arguments("C.sln")]
        [Arguments("C.slnx")]
        [Test]
        public async Task GetProjects_Should_ReturnArrayOfProjects_If_SolutionContainsProjectsThatDoExist(string solutionFile)
        {
            string[] projects = _fixture.CreateMany<string>().ToArray();
            CreateFiles(projects);
            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(projects));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            await Assert.That(result).IsEquivalentTo(projects.Select(_fileSystem.Path.GetFullPath), CollectionOrdering.InOrder);

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(_fileSystem.Path.GetFullPath(solutionFile));
        }

        [Arguments("A.sln")]
        [Arguments("B.sln")]
        [Arguments("C.sln")]
        [Arguments("A.slnx")]
        [Test]
        public async Task GetProjects_Should_ReturnOnlyExistingProjectsInSolutionFile(string solutionFile)
        {
            string[] existingProjects = _fixture.CreateMany<string>().ToArray();
            IEnumerable<string> missingProjects = _fixture.CreateMany<string>();

            CreateFiles(existingProjects);

            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(existingProjects.Concat(missingProjects).Shuffle(54321));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            await Assert.That(result).IsEquivalentTo(existingProjects.Select(_fileSystem.Path.GetFullPath), CollectionOrdering.Any);

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(_fileSystem.Path.GetFullPath(solutionFile));
        }

        [Test]
        public async Task GetProjectsFromSolution_Should_ReturnProjectsInActualSolutionFileRelativePath()
        {
            var solutionPersistance = new SolutionPersistanceWrapper();
            string solutionFolder = Path.GetFullPath("../../../../targets");
            string solutionFileName = "Projects.sln";
            IEnumerable<string> result = await solutionPersistance.GetProjectsFromSolutionAsync(_fileSystem.Path.Combine(solutionFolder, solutionFileName));

            await Assert.That(result.Select(_fileSystem.Path.IsPathRooted).All(b => b)).IsTrue();

            await Verify(string.Join(",", result.Select(p => GetPathRelativeTo(solutionFolder, p))), _osPlatformSpecificVerifySettings);
        }

        [Test]
        public async Task GetProjectsFromXmlSolution_Should_ReturnProjectsInActualSolutionFileRelativePath()
        {
            var solutionPersistance = new SolutionPersistanceWrapper();
            string solutionFolder = Path.GetFullPath("../../../../targets/slnx");
            string solutionFileName = "slnx.slnx";
            IEnumerable<string> result = await solutionPersistance.GetProjectsFromSolutionAsync(_fileSystem.Path.Combine(solutionFolder, solutionFileName));

            await Assert.That(result.Select(_fileSystem.Path.IsPathRooted).All(b => b)).IsTrue();

            await Verify(string.Join(",", result.Select(p => GetPathRelativeTo(solutionFolder, p))), _osPlatformSpecificVerifySettings);
        }

        private void CreateFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                _fileSystem.File.WriteAllBytes(file, Array.Empty<byte>());
            }
        }

        private string GetPathRelativeTo(string relativeTo, string path)
            => _fileSystem.Path.GetRelativePath(relativeTo, path);
    }
}
