// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Compression;

namespace NuGetUtility.Wrapper.ZipArchiveWrapper;

public class ZipArchiveAdapter(ZipArchive zipArchive) : IZipArchive
{
    private bool _disposed = false;

    public IZipArchiveEntry? GetEntry(string entryName)
    {
        ZipArchiveEntry? entry = zipArchive.GetEntry(entryName);
        return entry != null ? new ZipArchiveEntryWrapper(entry) : null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                zipArchive?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class ZipArchiveEntryWrapper(ZipArchiveEntry entry) : IZipArchiveEntry
{
    public Stream Open() => entry.Open();
}
