// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using NSubstitute;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

namespace NuGetUtility.Test.ReferencedPackagesReader
{
    [TestFixture]
    public class ProjectsCollectorTest
    {
        public ProjectsCollectorTest()
        {
            _osPlatformSpecificVerifySettings = new();
            _osPlatformSpecificVerifySettings.UniqueForOSPlatform();
        }

        [SetUp]
        public void SetUp()
        {
            _fixture = new Fixture();
            _solutionPersistanceWrapper = Substitute.For<ISolutionPersistanceWrapper>();
            _uut = new ProjectsCollector(_solutionPersistanceWrapper);
        }
        private ISolutionPersistanceWrapper _solutionPersistanceWrapper = null!;
        private ProjectsCollector _uut = null!;
        private Fixture _fixture = null!;
        private readonly VerifySettings _osPlatformSpecificVerifySettings;

        [TestCase("A.csproj")]
        [TestCase("B.fsproj")]
        [TestCase("C.vbproj")]
        [TestCase("D.dbproj")]
        public async Task GetProjects_Should_ReturnProjectsAsListDirectly(string projectFile)
        {
            IEnumerable<string> result = await _uut.GetProjectsAsync(projectFile);
            Assert.That(result, Is.EqualTo([Path.GetFullPath(projectFile)]));
            await _solutionPersistanceWrapper.DidNotReceive().GetProjectsFromSolutionAsync(Arg.Any<string>());
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("A.slnx")]
        public async Task GetProjects_Should_QueryMsBuildToGetProjectsForSolutionFiles(string solutionFile)
        {
            _ = await _uut.GetProjectsAsync(solutionFile);

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("C.slnx")]
        public async Task GetProjects_Should_ReturnEmptyArray_If_SolutionContainsNoProjects(string solutionFile)
        {
            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult(Enumerable.Empty<string>()));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.Empty);

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("B.slnx")]
        public async Task GetProjects_Should_ReturnEmptyArray_If_SolutionContainsProjectsThatDontExist(string solutionFile)
        {
            IEnumerable<string> projects = _fixture.CreateMany<string>();
            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult(projects));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.Empty);

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("C.slnx")]
        public async Task GetProjects_Should_ReturnArrayOfProjects_If_SolutionContainsProjectsThatDoExist(string solutionFile)
        {
            string[] projects = _fixture.CreateMany<string>().ToArray();
            CreateFiles(projects);
            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(projects));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.EqualTo(projects.Select(Path.GetFullPath)));

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("A.slnx")]
        public async Task GetProjects_Should_ReturnOnlyExistingProjectsInSolutionFile(string solutionFile)
        {
            string[] existingProjects = _fixture.CreateMany<string>().ToArray();
            IEnumerable<string> missingProjects = _fixture.CreateMany<string>();

            CreateFiles(existingProjects);

            _solutionPersistanceWrapper.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(existingProjects.Concat(missingProjects).Shuffle(54321));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.EquivalentTo(existingProjects.Select(Path.GetFullPath)));

            await _solutionPersistanceWrapper.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [Test]
        public async Task GetProjectsFromSolution_Should_ReturnProjectsInActualSolutionFileRelativePath()
        {
            var solutionPersistance = new SolutionPersistanceWrapper();
            string solutionFolder = Path.GetFullPath("../../../../targets");
            string solutionFileName = "Projects.sln";
            IEnumerable<string> result = await solutionPersistance.GetProjectsFromSolutionAsync(Path.Combine(solutionFolder, solutionFileName));

            Assert.That(result.Select(Path.IsPathRooted), Is.All.True);

            await Verify(string.Join(",", result.Select(p => GetPathRelativeTo(solutionFolder, p))), _osPlatformSpecificVerifySettings);
        }

        [Test]
        public async Task GetProjectsFromXmlSolution_Should_ReturnProjectsInActualSolutionFileRelativePath()
        {
            var solutionPersistance = new SolutionPersistanceWrapper();
            string solutionFolder = Path.GetFullPath("../../../../targets/slnx");
            string solutionFileName = "slnx.slnx";
            IEnumerable<string> result = await solutionPersistance.GetProjectsFromSolutionAsync(Path.Combine(solutionFolder, solutionFileName));

            Assert.That(result.Select(Path.IsPathRooted), Is.All.True);

            await Verify(string.Join(",", result.Select(p => GetPathRelativeTo(solutionFolder, p))), _osPlatformSpecificVerifySettings);
        }

        private static void CreateFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                File.WriteAllBytes(file, Array.Empty<byte>());
            }
        }

        private static string GetPathRelativeTo(string relativeTo, string path)
#if NETFRAMEWORK
        {
            // Require trailing backslash for path
            relativeTo = relativeTo.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relativeTo += Path.DirectorySeparatorChar;

            Uri baseUri = new Uri(relativeTo);
            Uri fullUri = new Uri(path);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            return relativeUri.ToString().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        }
#else
            => Path.GetRelativePath(relativeTo, path);
#endif
    }
}
