// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Extensions;

namespace NuGetUtility.Test.Extensions
{
    public class StringExtensionsTest
    {
        public class LikeTests
        {
            [Test]
            [Arguments("test", "test", true)]
            [Arguments("test", "TEST", true)]
            [Arguments("test", "t*", true)]
            [Arguments("test", "*st", true)]
            [Arguments("test", "t*st", true)]
            [Arguments("test", "t?st", true)]
            [Arguments("test", "t??t", true)]
            [Arguments("test", "????", true)]
            [Arguments("test", "*", true)]
            [Arguments("test", "?*", true)]
            [Arguments("test", "*?", true)]
            [Arguments("test", "fail", false)]
            [Arguments("test", "t?t", false)]
            [Arguments("test", "???", false)]
            [Arguments("test", "?????", false)]
            [Arguments("MyProject.csproj", "*.csproj", true)]
            [Arguments("MyProject.csproj", "MyProject.*", true)]
            [Arguments("MyProject.csproj", "My*.*", true)]
            [Arguments("MyProject.csproj", "*.vbproj", false)]
            public async Task Like_Should_MatchPattern(string input, string pattern, bool expected)
            {
                await Assert.That(input.Like(pattern)).IsEqualTo(expected);
            }
        }

        public class PathLikeTests
        {
            [Test]
            [Arguments("test", "test", true)]
            [Arguments("test", "TEST", true)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "*.csproj", true)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "MyProject.csproj", true)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "MyProject.*", true)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "*MyProject.csproj", true)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "C:\\Projects\\*\\*.csproj", true)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "*.vbproj", false)]
            [Arguments("C:\\Projects\\MyProject\\MyProject.csproj", "OtherProject.csproj", false)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "*.csproj", true)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "MyProject.csproj", true)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "MyProject.*", true)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "*MyProject.csproj", true)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "/home/user/*/MyProject/*.csproj", true)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "*.vbproj", false)]
            [Arguments("/home/user/projects/MyProject/MyProject.csproj", "OtherProject.csproj", false)]
            [Arguments("C:\\Projects\\Testing\\Test.pyproj", "*.pyproj", true)]
            [Arguments("C:\\Projects\\Testing\\Test.pyproj", "Test.pyproj", true)]
            [Arguments("C:\\Projects\\Testing\\Test.pyproj", "*Testing*", true)]
            [Arguments("C:\\Projects\\Mosaik.Testing.Something\\Project.csproj", "*Mosaik.Testing*", true)]
            [Arguments("C:\\Projects\\Mosaik.Testing.Something\\Project.csproj", "Project.csproj", true)]
            [Arguments("Project.Name.Test", "Project.Name*", true)]
            [Arguments("Some\\Path\\Project.Name.Test", "Project.Name*", true)]
            public async Task PathLike_Should_MatchPattern_AgainstFullPathOrFileName(string path, string pattern, bool expected)
            {
                await Assert.That(path.PathLike(pattern)).IsEqualTo(expected);
            }

            [Test]
            public async Task PathLike_Should_MatchFullPath_WhenPatternMatchesFullPath()
            {
                string path = "C:\\Projects\\MyProject\\MyProject.csproj";
                string pattern = "C:\\Projects\\*\\*.csproj";

                await Assert.That(path.PathLike(pattern)).IsTrue();
            }

            [Test]
            public async Task PathLike_Should_MatchFileName_WhenPatternMatchesFileName()
            {
                string path = "C:\\Projects\\MyProject\\MyProject.csproj";
                string pattern = "*.csproj";

                await Assert.That(path.PathLike(pattern)).IsTrue();
            }

            [Test]
            public async Task PathLike_Should_NotMatch_WhenNeitherFullPathNorFileNameMatches()
            {
                string path = "C:\\Projects\\MyProject\\MyProject.csproj";
                string pattern = "*.vbproj";

                await Assert.That(path.PathLike(pattern)).IsFalse();
            }

            [Test]
            [Arguments("", "*", true)]
            [Arguments("test", "", false)]
            public async Task PathLike_Should_HandleEdgeCases(string path, string pattern, bool expected)
            {
                await Assert.That(path.PathLike(pattern)).IsEqualTo(expected);
            }
        }
    }
}
