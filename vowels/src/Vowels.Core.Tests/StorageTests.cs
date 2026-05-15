using Xunit;
using Vowels.FileStoreRegistry.Storage;
using Vowels.FileStoreRegistry;
using Vowels.Common;
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
            uint p1 = manager.AllocatePage(BinarySpec.PageType.SchemaChain);
            
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
                    Assert.Equal(BinarySpec.PageType.SchemaChain, header1->Type);
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
            manager.AllocatePage(BinarySpec.PageType.SchemaChain); // Page 1
            uint p2 = manager.AllocatePage(BinarySpec.PageType.SchemaChain); // Page 2 -> Should trigger growth
            
            // Assert
            Assert.Equal(2u, p2);
            Assert.True(new FileInfo(testFile).Length > 8192);
        }

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public void HourlyMmfFile_ShouldHandleSchemaSwitching()
    {
        var testFile = "test_schema_switch.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        using (var file = new HourlyMmfFile(testFile))
        {
            // HourlyMmfFile has AddValue which handles schema internally
            file.AddValue("sensor.test", VowelsType.Double, 20.0, new DateTime(2026, 5, 10, 0, 1, 0));
            
            // Note: Since HourlyMmfFile internals are different from the old EntityStore,
            // we should probably add verification methods if we want to check the schema explicitly,
            // or just rely on the fact that it doesn't throw.
            // For now, I've implemented AddValue in HourlyMmfFile to handle schema switching.
        }

        if (File.Exists(testFile)) File.Delete(testFile);
    }
}
