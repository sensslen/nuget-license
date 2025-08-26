// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Compression;

namespace NuGetUtility.Wrapper.ZipArchiveWrapper;

public sealed class ZipArchiveWrapper : IZipArchiveWrapper
{
    public IZipArchive Open(Stream stream)
    {
        return new ZipArchiveAdapter(new ZipArchive(stream, ZipArchiveMode.Read));
    }
}
