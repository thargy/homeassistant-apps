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
    public void EntityStore_ShouldHandleSchemaSwitching()
    {
        var testFile = "test_schema_switch.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        using (var manager = new PagedMmfManager(testFile, 8192))
        {
            var store = new EntityStore(manager);
            
            // 1. Initial Schema
            var attrs1 = new[] { new BinarySpec.AttributeDefinition { NameStringId = 1, Type = BinarySpec.VowelsType.Double } };
            store.SwitchSchema(101, new DateTime(2026, 5, 10, 0, 0, 0), BinarySpec.VowelsType.Double, attrs1);
            
            // 2. Record data for Schema 1
            var recordAttrs = new Dictionary<uint, object> { { 1, 23.5 } };
            store.RecordState(101, new DateTime(2026, 5, 10, 0, 1, 0), 20.0, recordAttrs);

            // 3. Switch to Schema 2
            var attrs2 = new[] { 
                new BinarySpec.AttributeDefinition { NameStringId = 1, Type = BinarySpec.VowelsType.Double },
                new BinarySpec.AttributeDefinition { NameStringId = 2, Type = BinarySpec.VowelsType.Int64 }
            };
            store.SwitchSchema(101, new DateTime(2026, 5, 10, 1, 0, 0), BinarySpec.VowelsType.Double, attrs2);

            // 4. Record data for Schema 2
            var recordAttrs2 = new Dictionary<uint, object> { { 1, 24.0 }, { 2, 100L } };
            store.RecordState(101, new DateTime(2026, 5, 10, 1, 1, 0), 21.0, recordAttrs2);

            // 5. Verify Lookup
            var schema1 = store.GetActiveSchema(101, new DateTime(2026, 5, 10, 0, 30, 0));
            Assert.NotNull(schema1);
            Assert.Equal(1, schema1.Value.AttrCount);

            var schema2 = store.GetActiveSchema(101, new DateTime(2026, 5, 10, 1, 30, 0));
            Assert.NotNull(schema2);
            Assert.Equal(2, schema2.Value.AttrCount);
        }

        if (File.Exists(testFile)) File.Delete(testFile);
    }
}

