using Xunit;
using Vowels.Core.Storage;
using System.IO;
using System.Collections.Generic;

namespace Vowels.Core.Tests;

public class RegistryTests
{
    [Fact]
    public void StringTable_ShouldInternStrings()
    {
        var testFile = "test_strings.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        using (var manager = new PagedMmfManager(testFile, 8192))
        {
            uint stPage = manager.AllocatePage(BinarySpec.PageType.StringTable);
            var st = new StringTable(manager, stPage);

            uint id1 = st.GetOrAdd("sensor.test_1");
            uint id2 = st.GetOrAdd("sensor.test_2");
            uint id1_repeat = st.GetOrAdd("sensor.test_1");

            Assert.Equal(id1, id1_repeat);
            Assert.NotEqual(id1, id2);
        }

        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public void SchemaRegistry_ShouldRegisterSchemas()
    {
        var testFile = "test_schemas.vowl";
        if (File.Exists(testFile)) File.Delete(testFile);

        using (var manager = new PagedMmfManager(testFile, 8192))
        {
            uint stPage = manager.AllocatePage(BinarySpec.PageType.StringTable);
            uint srPage = manager.AllocatePage(BinarySpec.PageType.SchemaRegistry);
            
            var st = new StringTable(manager, stPage);
            var sr = new SchemaRegistry(manager, st, srPage);

            var schema1 = new[] { ("unit_of_measurement", BinarySpec.HaType.StringId), ("state_class", BinarySpec.HaType.StringId) };
            var schema2 = new[] { ("unit_of_measurement", BinarySpec.HaType.StringId), ("device_class", BinarySpec.HaType.StringId) };

            ushort id1 = sr.GetOrRegister(schema1);
            ushort id2 = sr.GetOrRegister(schema2);
            ushort id1_repeat = sr.GetOrRegister(schema1);

            Assert.Equal(id1, id1_repeat);
            Assert.NotEqual(id1, id2);
        }

        if (File.Exists(testFile)) File.Delete(testFile);
    }
}
