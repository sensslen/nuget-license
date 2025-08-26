// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Compression;

namespace NuGetUtility.Wrapper.ZipArchiveWrapper
{
    public interface IZipArchiveWrapper
    {
        IZipArchive Open(Stream stream);
    }
}

public interface IZipArchive : IDisposable
{
    IZipArchiveEntry? GetEntry(string entryName);
}

public interface IZipArchiveEntry
{
    Stream Open();
}
