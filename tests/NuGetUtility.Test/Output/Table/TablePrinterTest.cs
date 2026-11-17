// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Output.Table;

namespace NuGetUtility.Test.Output.Table
{
    public class TablePrinterTest
    {
        [Test]
        [Arguments(1)]
        [Arguments(3)]
        public async Task AddRow_Should_Throw_If_RowLengthDiffersFromTitleCount(int rowLength)
        {
            using var stream = new MemoryStream();
            TablePrinter uut = TablePrinterExtensions.Create(stream, "First", "Second");

            ArgumentException? exception = await Assert.That(() => uut.AddRow(new object?[rowLength]))
                .Throws<ArgumentException>();

            await Assert.That(exception!.Message).IsEqualTo($"Added row length [{rowLength}] is not equal to title row length [2]");
        }
    }
}
