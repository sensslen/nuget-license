// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Compression;

namespace NuGetUtility.Wrapper.ZipArchiveWrapper;

public class ZipArchiveAdapter(ZipArchive zipArchive) : IZipArchive
{
    public IZipArchiveEntry? GetEntry(string entryName)
    {
        ZipArchiveEntry? entry = zipArchive.GetEntry(entryName);
        return entry != null ? new ZipArchiveEntryWrapper(entry) : null;
    }

    public void Dispose() => zipArchive.Dispose();
}

public class ZipArchiveEntryWrapper(ZipArchiveEntry entry) : IZipArchiveEntry
{
    public Stream Open() => entry.Open();
}
