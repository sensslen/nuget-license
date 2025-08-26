// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Compression;
using System.Text;
using NuGetUtility.Wrapper.ZipArchiveWrapper;

namespace NuGetUtility.Test.Wrappers;

public class ZipArchiveWrapperTests
{
    [Test]
    public void Open_WithValidStream_ReturnsZipArchive()
    {
        // Arrange
        var wrapper = new ZipArchiveWrapper();
        using MemoryStream memoryStream = CreateTestZipStream();

        // Act
        using IZipArchive archive = wrapper.Open(memoryStream);

        // Assert
        Assert.That(archive, Is.Not.Null);
        Assert.That(archive, Is.InstanceOf<IZipArchive>());
    }

    [Test]
    public void Open_WithEmptyZipStream_ReturnsZipArchive()
    {
        // Arrange
        var wrapper = new ZipArchiveWrapper();
        using MemoryStream memoryStream = CreateEmptyZipStream();

        // Act
        using IZipArchive archive = wrapper.Open(memoryStream);

        // Assert
        Assert.That(archive, Is.Not.Null);
    }

    [Test]
    public void ZipArchiveAdapter_GetEntry_WithExistingEntry_ReturnsEntry()
    {
        // Arrange
        using MemoryStream memoryStream = CreateTestZipStream();
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var adapter = new ZipArchiveAdapter(zipArchive);

        // Act
        IZipArchiveEntry? entry = adapter.GetEntry("test.txt");

        // Assert
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry, Is.InstanceOf<IZipArchiveEntry>());
    }

    [Test]
    public void ZipArchiveAdapter_GetEntry_WithNonExistentEntry_ReturnsNull()
    {
        // Arrange
        using MemoryStream memoryStream = CreateTestZipStream();
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var adapter = new ZipArchiveAdapter(zipArchive);

        // Act
        IZipArchiveEntry? entry = adapter.GetEntry("nonexistent.txt");

        // Assert
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void ZipArchiveAdapter_Dispose_DisposesUnderlyingZipArchive()
    {
        // Arrange
        using MemoryStream memoryStream = CreateTestZipStream();
        var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var adapter = new ZipArchiveAdapter(zipArchive);

        // Act
        adapter.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => zipArchive.GetEntry("test.txt"));
    }

    [Test]
    public void ZipArchiveEntryWrapper_Open_ReturnsStream()
    {
        // Arrange
        using MemoryStream memoryStream = CreateTestZipStream();
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        ZipArchiveEntry zipEntry = zipArchive.GetEntry("test.txt")!;
        var entryWrapper = new ZipArchiveEntryWrapper(zipEntry);

        // Act
        using Stream stream = entryWrapper.Open();

        // Assert
        Assert.That(stream, Is.Not.Null);
        Assert.That(stream.CanRead, Is.True);
    }

    [Test]
    public void ZipArchiveEntryWrapper_Open_CanReadContent()
    {
        // Arrange
        using MemoryStream memoryStream = CreateTestZipStream();
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        ZipArchiveEntry zipEntry = zipArchive.GetEntry("test.txt")!;
        var entryWrapper = new ZipArchiveEntryWrapper(zipEntry);

        // Act
        using Stream stream = entryWrapper.Open();
        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();

        // Assert
        Assert.That(content, Is.EqualTo("test content"));
    }

    [Test]
    public void FullWorkflow_OpenZipAndReadEntry_WorksCorrectly()
    {
        // Arrange
        var wrapper = new ZipArchiveWrapper();
        using MemoryStream memoryStream = CreateTestZipStreamWithMultipleFiles();

        // Act
        using IZipArchive archive = wrapper.Open(memoryStream);
        IZipArchiveEntry? entry1 = archive.GetEntry("file1.txt");
        IZipArchiveEntry? entry2 = archive.GetEntry("file2.txt");
        IZipArchiveEntry? nonExistentEntry = archive.GetEntry("nonexistent.txt");

        // Assert
        Assert.That(entry1, Is.Not.Null);
        Assert.That(entry2, Is.Not.Null);
        Assert.That(nonExistentEntry, Is.Null);

        // Verify content of first file
        using Stream stream1 = entry1!.Open();
        using var reader1 = new StreamReader(stream1);
        string content1 = reader1.ReadToEnd();
        Assert.That(content1, Is.EqualTo("content of file 1"));

        // Verify content of second file
        using Stream stream2 = entry2!.Open();
        using var reader2 = new StreamReader(stream2);
        string content2 = reader2.ReadToEnd();
        Assert.That(content2, Is.EqualTo("content of file 2"));
    }

    private static MemoryStream CreateTestZipStream()
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("test.txt");
            using Stream entryStream = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes("test content");
            entryStream.Write(bytes, 0, bytes.Length);
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static MemoryStream CreateEmptyZipStream()
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Create empty zip
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static MemoryStream CreateTestZipStreamWithMultipleFiles()
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Create first file
            ZipArchiveEntry entry1 = archive.CreateEntry("file1.txt");
            using (Stream entryStream1 = entry1.Open())
            {
                byte[] bytes1 = Encoding.UTF8.GetBytes("content of file 1");
                entryStream1.Write(bytes1, 0, bytes1.Length);
            }

            // Create second file
            ZipArchiveEntry entry2 = archive.CreateEntry("file2.txt");
            using (Stream entryStream2 = entry2.Open())
            {
                byte[] bytes2 = Encoding.UTF8.GetBytes("content of file 2");
                entryStream2.Write(bytes2, 0, bytes2.Length);
            }
        }
        memoryStream.Position = 0;
        return memoryStream;
    }
}





