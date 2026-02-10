// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Extensions;

namespace NuGetUtility.Test.Extensions
{
    [TestFixture]
    internal class StringExtensionsTest
    {
        [TestFixture]
        internal class LikeTests
        {
            [TestCase("test", "test", true)]
            [TestCase("test", "TEST", true)]
            [TestCase("test", "t*", true)]
            [TestCase("test", "*st", true)]
            [TestCase("test", "t*st", true)]
            [TestCase("test", "t?st", true)]
            [TestCase("test", "t??t", true)]
            [TestCase("test", "????", true)]
            [TestCase("test", "*", true)]
            [TestCase("test", "?*", true)]
            [TestCase("test", "*?", true)]
            [TestCase("test", "fail", false)]
            [TestCase("test", "t?t", false)]
            [TestCase("test", "???", false)]
            [TestCase("test", "?????", false)]
            [TestCase("MyProject.csproj", "*.csproj", true)]
            [TestCase("MyProject.csproj", "MyProject.*", true)]
            [TestCase("MyProject.csproj", "My*.*", true)]
            [TestCase("MyProject.csproj", "*.vbproj", false)]
            public void Like_Should_MatchPattern(string input, string pattern, bool expected)
            {
                Assert.That(input.Like(pattern), Is.EqualTo(expected));
            }
        }

        [TestFixture]
        internal class PathLikeTests
        {
            [TestCase("test", "test", true)]
            [TestCase("test", "TEST", true)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "*.csproj", true)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "MyProject.csproj", true)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "MyProject.*", true)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "*MyProject.csproj", true)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "C:\\Projects\\*\\*.csproj", true)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "*.vbproj", false)]
            [TestCase("C:\\Projects\\MyProject\\MyProject.csproj", "OtherProject.csproj", false)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "*.csproj", true)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "MyProject.csproj", true)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "MyProject.*", true)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "*MyProject.csproj", true)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "/home/user/*/MyProject/*.csproj", true)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "*.vbproj", false)]
            [TestCase("/home/user/projects/MyProject/MyProject.csproj", "OtherProject.csproj", false)]
            [TestCase("C:\\Projects\\Testing\\Test.pyproj", "*.pyproj", true)]
            [TestCase("C:\\Projects\\Testing\\Test.pyproj", "Test.pyproj", true)]
            [TestCase("C:\\Projects\\Testing\\Test.pyproj", "*Testing*", true)]
            [TestCase("C:\\Projects\\Mosaik.Testing.Something\\Project.csproj", "*Mosaik.Testing*", true)]
            [TestCase("C:\\Projects\\Mosaik.Testing.Something\\Project.csproj", "Project.csproj", true)]
            [TestCase("Project.Name.Test", "Project.Name*", true)]
            [TestCase("Some\\Path\\Project.Name.Test", "Project.Name*", true)]
            public void PathLike_Should_MatchPattern_AgainstFullPathOrFileName(string path, string pattern, bool expected)
            {
                Assert.That(path.PathLike(pattern), Is.EqualTo(expected));
            }

            [Test]
            public void PathLike_Should_MatchFullPath_WhenPatternMatchesFullPath()
            {
                string path = "C:\\Projects\\MyProject\\MyProject.csproj";
                string pattern = "C:\\Projects\\*\\*.csproj";

                Assert.That(path.PathLike(pattern), Is.True);
            }

            [Test]
            public void PathLike_Should_MatchFileName_WhenPatternMatchesFileName()
            {
                string path = "C:\\Projects\\MyProject\\MyProject.csproj";
                string pattern = "*.csproj";

                Assert.That(path.PathLike(pattern), Is.True);
            }

            [Test]
            public void PathLike_Should_NotMatch_WhenNeitherFullPathNorFileNameMatches()
            {
                string path = "C:\\Projects\\MyProject\\MyProject.csproj";
                string pattern = "*.vbproj";

                Assert.That(path.PathLike(pattern), Is.False);
            }

            [TestCase("", "*", true)]
            [TestCase("test", "", false)]
            public void PathLike_Should_HandleEdgeCases(string path, string pattern, bool expected)
            {
                Assert.That(path.PathLike(pattern), Is.EqualTo(expected));
            }
        }
    }
}
