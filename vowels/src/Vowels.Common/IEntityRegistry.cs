namespace Vowels.Common;

public interface IEntityRegistry
{
    IObservable<EntityValue> GetEntityValues(IObservable<IEntityRequest> requests, DateTime start, DateTime end);
    IObservable<EntityValue> SetEntityValues(IObservable<EntityValue> values);
    
    // Utility overloads can be added here as extension methods or part of the interface
    // Example: SensorHandle GetHandle<T>(string entityId)
}
