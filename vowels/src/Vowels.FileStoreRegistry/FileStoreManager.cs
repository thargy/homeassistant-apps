using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.IO;
using Vowels.Common;
using Vowels.Common.Storage;

namespace Vowels.FileStoreRegistry;

public class FileStoreManager : IDataWriter, IDisposable
{
    private readonly string _storagePath;
    private readonly TimeSpan _segmentDuration;
    private readonly Dictionary<string, DataSegment> _openSegments = new();
    private readonly object _lock = new();

    public FileStoreManager(string storagePath, TimeSpan segmentDuration)
    {
        _storagePath = storagePath;
        _segmentDuration = segmentDuration;
        if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath);
    }

    public IEnumerable<string> GetKnownEntityIds()
    {
        var discovered = new HashSet<string>();
        var files = Directory.GetFiles(_storagePath, "vowels_*.vowl");
        foreach (var filePath in files)
        {
            try
            {
                // In a real scenario we'd parse the anchor from the filename, but for now we just pass dummy values to read the directory.
                using var file = new DataSegment(filePath, DateTimeOffset.MinValue, _segmentDuration);
                foreach (var id in file.GetKnownEntityIds()) discovered.Add(id);
            }
            catch { }
        }
        return discovered;
    }

    public IObservable<EntityValue> GetValues(IEnumerable<string> entityIds, DateTimeOffset start, DateTimeOffset end)
    {
        var observables = new List<IObservable<EntityValue>>();
        // Simple align to segment duration
        long ticks = start.Ticks - (start.Ticks % _segmentDuration.Ticks);
        var current = new DateTimeOffset(ticks, TimeSpan.Zero);
        
        while (current <= end)
        {
            var segment = GetSegmentForTime(current);
            observables.Add(segment.GetValues(entityIds, start, end));
            current = current.Add(_segmentDuration);
        }
        return observables.Concat();
    }

    public void SaveValues(IEnumerable<EntityValue> values)
    {
        // Group values by the segment they belong to
        var grouped = values.GroupBy(v => {
            long ticks = v.Timestamp.Ticks - (v.Timestamp.Ticks % _segmentDuration.Ticks);
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        });

        foreach (var group in grouped)
        {
            var segment = GetSegmentForTime(group.Key);
            segment.SaveValues(group);
        }
    }

    private DataSegment GetSegmentForTime(DateTimeOffset anchor)
    {
        string fileName = $"vowels_{anchor:yyyyMMdd_HHmmss}.vowl";
        string filePath = Path.Combine(_storagePath, fileName);

        lock (_lock)
        {
            if (_openSegments.TryGetValue(filePath, out var segment)) return segment;
            var newSegment = new DataSegment(filePath, anchor, _segmentDuration);
            _openSegments[filePath] = newSegment;
            return newSegment;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var segment in _openSegments.Values) segment.Dispose();
            _openSegments.Clear();
        }
    }
}
