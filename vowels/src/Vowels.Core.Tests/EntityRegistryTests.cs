using Xunit;
using Vowels.Core.Registry;
using Vowels.Core.Storage;
using System.IO;

namespace Vowels.Core.Tests;

public class EntityRegistryTests
{
    private const string TestDb = "registry_test.vowl";

    [Fact]
    public void RegisterEntity_ShouldCreateNewEntityId()
    {
        // Arrange
        if (File.Exists(TestDb)) File.Delete(TestDb);
        using var manager = new PagedMmfManager(TestDb, 8192);
        var store = new EntityStore(manager);
        
        var registry = new EntityRegistry(store);

        // Act
        registry.RegisterEntity("sensor.living_room_temperature");

        // Assert
        Assert.True(registry.IsEntityRegistered("sensor.living_room_temperature"));
        Assert.Contains(registry.Entities, e => e.EntityId == "sensor.living_room_temperature");
        
        if (File.Exists(TestDb)) File.Delete(TestDb);
    }
}
