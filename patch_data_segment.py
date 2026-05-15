import re

with open("vowels/src/Vowels.FileStoreRegistry/DataSegment.cs", "r") as f:
    content = f.read()

content = content.replace("internal class HourlyMmfFile : IDisposable", "using Vowels.Common.Storage;\n\ninternal class DataSegment : IDataWriter, IDisposable")
content = content.replace("public HourlyMmfFile(string filePath)", """private readonly DateTimeOffset _anchor;
    private readonly TimeSpan _duration;

    public DataSegment(string filePath, DateTimeOffset anchor, TimeSpan duration)""")

content = content.replace("""    public DataSegment(string filePath, DateTimeOffset anchor, TimeSpan duration)
    {""", """    public DataSegment(string filePath, DateTimeOffset anchor, TimeSpan duration)
    {
        _anchor = anchor;
        _duration = duration;""")

# Add SaveValues method
save_values_method = """
    public void SaveValues(IEnumerable<EntityValue> values)
    {
        foreach (var value in values)
        {
            AddValue(value.Handle.EntityId, value.Type, value.Value, value.Timestamp);
        }
    }
"""

content = content.replace("public void AddValue(", save_values_method + "\n    public void AddValue(")

# Change GetValues signature
content = content.replace("public IEnumerable<EntityValue> GetValues(IEnumerable<IHandle> handles, DateTime startTime, DateTime endTime)", "public IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end)")

# We need to change the inside of GetValues to use IObservable.
# It currently uses `yield return`.
content = content.replace("public IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end)\n    {", """public IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end)
    {
        return System.Reactive.Linq.Observable.Create<EntityValue>(observer => {
            foreach (var v in GetValuesInternal(entityIds, start, end)) { observer.OnNext(v); }
            observer.OnCompleted();
            return System.Reactive.Disposables.Disposable.Empty;
        });
    }

    private IEnumerable<EntityValue> GetValuesInternal(IEnumerable<string> entityIds, DateTimeOffset startTime, DateTimeOffset endTime)
    {""")

content = content.replace("foreach (var handle in handles)", "foreach (var entityId in entityIds)")
content = content.replace("uint entityIdStringId = _stringTable.GetId(handle.EntityId);", "uint entityIdStringId = _stringTable.GetId(entityId);")
content = content.replace("if (handle is SensorAttributeHandle attrHandle)", "if (false) // Attribute handles removed for now in this refactor")
content = content.replace("handle,", "new Vowels.Common.SensorHandle(entityId),")

with open("vowels/src/Vowels.FileStoreRegistry/DataSegment.cs", "w") as f:
    f.write(content)
