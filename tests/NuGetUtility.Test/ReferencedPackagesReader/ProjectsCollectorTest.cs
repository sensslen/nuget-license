// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using NSubstitute;
using NuGetUtility.ReferencedPackagesReader;
using NuGetUtility.Test.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.MsBuildWrapper;

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
            _msBuild = Substitute.For<IMsBuildAbstraction>();
            _uut = new ProjectsCollector(_msBuild);
        }
        private IMsBuildAbstraction _msBuild = null!;
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
            Assert.That(result, Is.EqualTo(new[] { Path.GetFullPath(projectFile) }));
            await _msBuild.DidNotReceive().GetProjectsFromSolutionAsync(Arg.Any<string>());
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("A.slnx")]
        public async Task GetProjects_Should_QueryMsBuildToGetProjectsForSolutionFiles(string solutionFile)
        {
            _ = await _uut.GetProjectsAsync(solutionFile);

            await _msBuild.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("C.slnx")]
        public async Task GetProjects_Should_ReturnEmptyArray_If_SolutionContainsNoProjects(string solutionFile)
        {
            _msBuild.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult(Enumerable.Empty<string>()));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.Empty);

            await _msBuild.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("B.slnx")]
        public async Task GetProjects_Should_ReturnEmptyArray_If_SolutionContainsProjectsThatDontExist(string solutionFile)
        {
            IEnumerable<string> projects = _fixture.CreateMany<string>();
            _msBuild.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult(projects));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.Empty);

            await _msBuild.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [TestCase("A.sln")]
        [TestCase("B.sln")]
        [TestCase("C.sln")]
        [TestCase("C.slnx")]
        public async Task GetProjects_Should_ReturnArrayOfProjects_If_SolutionContainsProjectsThatDoExist(string solutionFile)
        {
            string[] projects = _fixture.CreateMany<string>().ToArray();
            CreateFiles(projects);
            _msBuild.GetProjectsFromSolutionAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<string>>(projects));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.EqualTo(projects.Select(Path.GetFullPath)));

            await _msBuild.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
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

            _msBuild.GetProjectsFromSolutionAsync(Arg.Any<string>())
                .Returns(existingProjects.Concat(missingProjects).Shuffle(54321));

            IEnumerable<string> result = await _uut.GetProjectsAsync(solutionFile);
            Assert.That(result, Is.EquivalentTo(existingProjects.Select(Path.GetFullPath)));

            await _msBuild.Received(1).GetProjectsFromSolutionAsync(Path.GetFullPath(solutionFile));
        }

        [Test]
        public async Task GetProjectsFromSolution_Should_ReturnProjectsInActualSolutionFileRelativePath()
        {
            var msbuild = new MsBuildAbstraction();
            IEnumerable<string> result = await msbuild.GetProjectsFromSolutionAsync("../../../../targets/Projects.sln");
            await Verify(string.Join(",", result), _osPlatformSpecificVerifySettings);
        }

        [Test]
        public async Task GetProjectsFromXmlSolution_Should_ReturnProjectsInActualSolutionFileRelativePath()
        {
            var msbuild = new MsBuildAbstraction();
            IEnumerable<string> result = await msbuild.GetProjectsFromSolutionAsync("../../../../targets/slnx/slnx.slnx");
            await Verify(string.Join(",", result), _osPlatformSpecificVerifySettings);
        }

        private static void CreateFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                File.WriteAllBytes(file, Array.Empty<byte>());
            }
        }
    }
}
