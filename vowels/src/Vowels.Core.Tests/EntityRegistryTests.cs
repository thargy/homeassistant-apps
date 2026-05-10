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
        EntityStore.ResetForTesting();
        EntityRegistry.ResetForTesting();

        using (var manager = new PagedMmfManager(TestDb, 8192))
        {
            EntityStore.Initialize(manager);
            EntityRegistry.Initialize(EntityStore.Instance);
            
            var registry = EntityRegistry.Instance;

            // Act
            registry.RegisterEntity("sensor.living_room_temperature");

            // Assert
            Assert.True(registry.IsEntityRegistered("sensor.living_room_temperature"));
            Assert.Contains(registry.Entities, e => e.EntityId == "sensor.living_room_temperature");
        }
        
        if (File.Exists(TestDb)) File.Delete(TestDb);
    }
}
