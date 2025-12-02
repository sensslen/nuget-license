// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using FileLicenseMatcher.Compare;
using NSubstitute;

namespace FileLicenseMatcher.Test.Compare
{
    public class LicenseMatcherTest
    {
        private readonly IFileSystem _fileSystem;

        public LicenseMatcherTest()
        {
            _fileSystem = Substitute.For<IFileSystem>();
        }

#pragma warning disable S6966 //Awaitable method should be used
        [Test]
        public async Task Match_Should_Return_Empty_When_Map_Is_Empty()
        {
            var map = new Dictionary<string, string>();
            var uut = new LicenseMatcher(_fileSystem, map);

            string result = uut.Match("any");

            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task Match_Should_Return_Empty_When_File_Does_Not_Exist()
        {
            var map = new Dictionary<string, string>
            {
                ["/path/to/license.txt"] = "LIC-1"
            };
            IFile file = Substitute.For<IFile>();
            _fileSystem.File.Returns(file);
            file.Exists("/path/to/license.txt").Returns(false);

            var uut = new LicenseMatcher(_fileSystem, map);

            string result = uut.Match("license contents");

            await Assert.That(result).IsEmpty();
            file.DidNotReceive().ReadAllText(Arg.Any<string>());
        }

        [Test]
        public async Task Match_Should_Return_Mapped_Id_When_Content_Equals()
        {
            const string path = "/path/to/license.txt";
            const string mappedId = "MIT";
            const string content = "license contents";

            var map = new Dictionary<string, string>
            {
                [path] = mappedId
            };

            IFile file = Substitute.For<IFile>();
            _fileSystem.File.Returns(file);
            file.Exists(path).Returns(true);
            file.ReadAllText(path).Returns(content);

            var uut = new LicenseMatcher(_fileSystem, map);

            string result = uut.Match(content);

            await Assert.That(result).EqualTo(mappedId);
            file.Received(1).ReadAllText(path);
        }

        [Test]
        public async Task Match_Should_Skip_Files_That_Do_Not_Exist_And_Return_When_Found()
        {
            const string firstPath = "/no/such/file.txt";
            const string secondPath = "/exists/file.txt";
            const string secondId = "Apache-2.0";
            const string licenseText = "apache license text";

            var map = new Dictionary<string, string>
            {
                [firstPath] = "SOME-1",
                [secondPath] = secondId
            };

            IFile file = Substitute.For<IFile>();
            _fileSystem.File.Returns(file);
            file.Exists(firstPath).Returns(false);
            file.Exists(secondPath).Returns(true);
            file.ReadAllText(secondPath).Returns(licenseText);

            var uut = new LicenseMatcher(_fileSystem, map);

            string result = uut.Match(licenseText);

            await Assert.That(result).EqualTo(secondId);
            file.DidNotReceive().ReadAllText(firstPath);
            file.Received(1).ReadAllText(secondPath);
        }

        [Test]
        public async Task Match_Should_Return_Empty_When_No_File_Matches_Content()
        {
            const string path = "/path/to/license.txt";

            var map = new Dictionary<string, string>
            {
                [path] = "BSD-2-Clause"
            };

            IFile file = Substitute.For<IFile>();
            _fileSystem.File.Returns(file);
            file.Exists(path).Returns(true);
            file.ReadAllText(path).Returns("different content");

            var uut = new LicenseMatcher(_fileSystem, map);

            string result = uut.Match("license contents");

            await Assert.That(result).IsEmpty();
            file.Received(1).ReadAllText(path);
        }

        [Test]
        public async Task Match_Should_Ignore_Whitespace_Differences()
        {
            const string path = "/path/to/license.txt";
            const string mappedId = "MIT";
            const string fileContent = "license   contents\nwith\tmultiple   whitespace";
            const string inputContent = "license contents with multiple whitespace";

            var map = new Dictionary<string, string>
            {
                [path] = mappedId
            };

            IFile file = Substitute.For<IFile>();
            _fileSystem.File.Returns(file);
            file.Exists(path).Returns(true);
            file.ReadAllText(path).Returns(fileContent);

            var uut = new LicenseMatcher(_fileSystem, map);

            string result = uut.Match(inputContent);

            await Assert.That(result).EqualTo(mappedId);
            file.Received(1).ReadAllText(path);
        }
#pragma warning restore S6966 //Awaitable method should be used
    }
}
