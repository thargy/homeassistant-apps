using Xunit;
using Vowels.Core.Storage;
using System.IO;

namespace Vowels.Core.Tests;

public class StorageTests
{
    [Fact]
    public void PagedMmfManager_ShouldAllocateAndLinkPages()
    {
        // Arrange
        var testFile = "test_storage.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        using (var manager = new PagedMmfManager(testFile, 8192))
        {
            // Act
            uint p0 = manager.AllocatePage(BinarySpec.PageType.Header);
            uint p1 = manager.AllocatePage(BinarySpec.PageType.EntityData);
            
            // Assert
            Assert.Equal(0u, p0);
            Assert.Equal(1u, p1);

            var span0 = manager.GetPageSpan(p0);
            var span1 = manager.GetPageSpan(p1);

            unsafe
            {
                fixed (byte* ptr0 = span0)
                {
                    var header0 = (BinarySpec.PageHeader*)ptr0;
                    Assert.Equal(BinarySpec.PageType.Header, header0->Type);
                }
                fixed (byte* ptr1 = span1)
                {
                    var header1 = (BinarySpec.PageHeader*)ptr1;
                    Assert.Equal(BinarySpec.PageType.EntityData, header1->Type);
                }
            }
        }

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public void PagedMmfManager_ShouldGrowAutomatically()
    {
        // Arrange
        var testFile = "test_grow.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        // Initial size: 2 pages (8KB)
        using (var manager = new PagedMmfManager(testFile, 8192))
        {
            // Act
            manager.AllocatePage(BinarySpec.PageType.Header); // Page 0
            manager.AllocatePage(BinarySpec.PageType.EntityData); // Page 1
            uint p2 = manager.AllocatePage(BinarySpec.PageType.EntityData); // Page 2 -> Should trigger growth
            
            // Assert
            Assert.Equal(2u, p2);
            Assert.True(new FileInfo(testFile).Length > 8192);
        }

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public void EntityStore_ShouldHandleSchemaSwitching()
    {
        var testFile = "test_schema_switch.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        using (var manager = new PagedMmfManager(testFile, 8192))
        {
            var store = new EntityStore(manager);
            var data = new byte[] { 1, 2, 3, 4 };
            
            // 1. Record with Schema 1
            store.RecordState(101, 1, 255, 1000, data);
            
            // 2. Record with Schema 2 (Switch)
            store.RecordState(101, 2, 255, 2000, data);

            var span = manager.GetPageSpan(1); // Page 1 is the first data page
            // Find the marker, skip headers (17 bytes)
            bool foundMarker = false;
            for (int i = 17; i < span.Length - 7; i++)
            {
                if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i, 4)) == uint.MaxValue)
                {
                    foundMarker = true;
                    Assert.Equal(0x01, span[i + 4]); // MetaType
                    // Next 2 bytes should be the new Schema ID (2)
                    Assert.Equal(2, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i + 5, 2)));
                    break;
                }
            }
            Assert.True(foundMarker);
        }

        if (File.Exists(testFile)) File.Delete(testFile);
    }
}
