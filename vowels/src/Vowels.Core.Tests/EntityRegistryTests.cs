using Xunit;
using Vowels.Core.Registry;
using Vowels.FileStoreRegistry;
using Vowels.Core.Common;
using System.IO;

namespace Vowels.Core.Tests;

public class EntityRegistryTests
{
    private const string TestDir = "registry_test_data";

    [Fact]
    public void RegisterEntity_ShouldCreateNewEntityId()
    {
        // Arrange
        if (Directory.Exists(TestDir)) Directory.Delete(TestDir, true);
        Directory.CreateDirectory(TestDir);
        
        EntityRegistry.ResetForTesting();

        var store = new FileStoreManager(TestDir);
        EntityRegistry.Initialize(store);
        
        var registry = EntityRegistry.Instance;

        // Act
        registry.RegisterEntity("sensor.living_room_temperature");

        // Assert
        Assert.True(registry.IsEntityRegistered("sensor.living_room_temperature"));
        Assert.Contains(registry.Entities, e => e.EntityId == "sensor.living_room_temperature");
        
        // Cleanup
        if (Directory.Exists(TestDir)) Directory.Delete(TestDir, true);
    }
}
