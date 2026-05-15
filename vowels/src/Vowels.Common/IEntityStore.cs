namespace Vowels.Common;

public interface IEntityStore
{
    IObservable<EntityValue> GetValues(IEnumerable<IHandle> handles, DateTime start, DateTime end);
    IObservable<EntityValue> SaveValues(IObservable<EntityValue> values);
    
    // Allows the store to tell the registry about entities it found in history
    IObservable<IHandle> DiscoverHistoricalHandles(IEntityRequest request);
}
