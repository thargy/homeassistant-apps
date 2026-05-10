using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Linq;
using Vowels.Core.Common;

namespace Vowels.FileStoreRegistry;

public class FileStoreManager : IEntityStore, IDisposable
{
    private readonly string _storagePath;
    private readonly Dictionary<string, HourlyMmfFile> _openFiles = new();
    private readonly object _lock = new();

    public FileStoreManager(string storagePath)
    {
        _storagePath = storagePath;
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public IObservable<IHandle> DiscoverHistoricalHandles(IEntityRequest request)
    {
        return Observable.Create<IHandle>(observer =>
        {
            var discovered = new HashSet<string>();
            var files = Directory.GetFiles(_storagePath, "vowels_*.vowl");
            foreach (var filePath in files)
            {
                try
                {
                    using var file = new HourlyMmfFile(filePath);
                    foreach (var entityId in file.GetKnownEntityIds())
                    {
                        if (Matches(entityId, request) && discovered.Add(entityId))
                        {
                            // For now we assume SensorHandle, but in future we might store the type in the directory
                            observer.OnNext(new SensorHandle(entityId));
                        }
                    }
                }
                catch
                {
                    // Skip corrupted or inaccessible files
                }
            }
            observer.OnCompleted();
            return System.Reactive.Disposables.Disposable.Empty;
        });
    }

    private bool Matches(string entityId, IEntityRequest request)
    {
        if (request is EntityIDRequest idReq) return entityId == idReq.EntityId;
        if (request is EntitiesRegexRequest regexReq) return Regex.IsMatch(entityId, regexReq.Pattern);
        if (request is IHandle handle) return entityId == handle.EntityId;
        return false;
    }

    public IObservable<EntityValue> GetValues(IEnumerable<IHandle> handles, DateTime start, DateTime end)
    {
        var observables = new List<IObservable<EntityValue>>();
        var current = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
        
        while (current <= end)
        {
            var file = GetFileForTime(current);
            observables.Add(file.GetValues(handles, start, end).ToObservable());
            current = current.AddHours(1);
        }

        return observables.Concat();
    }

    public IObservable<EntityValue> SaveValues(IObservable<EntityValue> values)
    {
        return values.Do(value =>
        {
            var file = GetFileForTime(value.Timestamp);
            file.AddValue(value.Handle.EntityId, value.Type, value.Value, value.Timestamp);
        });
    }

    private HourlyMmfFile GetFileForTime(DateTime time)
    {
        string fileName = $"vowels_{time:yyyyMMdd_HH}.vowl";
        string filePath = Path.Combine(_storagePath, fileName);

        lock (_lock)
        {
            if (_openFiles.TryGetValue(filePath, out var file))
            {
                return file;
            }

            // TODO: Implement LRA cache/eviction if too many files are open
            var newFile = new HourlyMmfFile(filePath);
            _openFiles[filePath] = newFile;
            return newFile;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var file in _openFiles.Values)
            {
                file.Dispose();
            }
            _openFiles.Clear();
        }
    }
}
