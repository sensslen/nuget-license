// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using FileLicenseMatcher.Combine;
using NSubstitute;

namespace FileLicenseMatcher.Test.Combine
{
    public class LicenseMatcherTest
    {
        private readonly IFileLicenseMatcher _first;
        private readonly IFileLicenseMatcher _second;

        public LicenseMatcherTest()
        {
            _first = Substitute.For<IFileLicenseMatcher>();
            _second = Substitute.For<IFileLicenseMatcher>();
        }

        [Test]
        public async Task Match_Should_Return_Empty_If_No_Matchers()
        {
            var uut = new LicenseMatcher(new List<IFileLicenseMatcher>());

            string result = uut.Match("text");

            await Assert.That(result).IsEmpty();
        }

        [Test]
        public async Task Match_Should_Return_First_NonEmpty_Match()
        {
            _first.Match(Arg.Any<string>()).Returns(string.Empty);
            _second.Match(Arg.Any<string>()).Returns("MIT");

            var uut = new LicenseMatcher(new List<IFileLicenseMatcher> { _first, _second });

            string result = uut.Match("anything");

            await Assert.That(result).EqualTo("MIT");
            _first.Received(1).Match("anything");
            _second.Received(1).Match("anything");
        }

        [Test]
        public async Task Match_Should_Stop_At_First_Match()
        {
            _first.Match(Arg.Any<string>()).Returns("Apache-2.0");
            _second.Match(Arg.Any<string>()).Returns("MIT");

            var uut = new LicenseMatcher(new List<IFileLicenseMatcher> { _first, _second });

            string result = uut.Match("anything");

            await Assert.That(result).EqualTo("Apache-2.0");
            _first.Received(1).Match("anything");
            _second.DidNotReceive().Match(Arg.Any<string>());
        }
    }
}
