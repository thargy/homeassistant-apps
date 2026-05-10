using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace Vowels.Core.Storage;

public partial class EntityStore
{
    private class StringTable
    {
        private readonly IPageManager _pageManager;
        private readonly Dictionary<string, uint> _stringToId = new();
        private readonly Dictionary<uint, string> _idToString = new();
        private uint _currentPageId;
        private int _currentOffset;
        private uint _nextId = 1;

        public StringTable(IPageManager pageManager, uint headPageId)
        {
            _pageManager = pageManager;
            _currentPageId = headPageId;
            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            uint current = _currentPageId;
            while (current != 0)
            {
                var span = _pageManager.GetPageSpan(current);
                var header = MemoryMarshal.Read<BinarySpec.PageHeader>(span);
                
                int offset = Marshal.SizeOf<BinarySpec.PageHeader>();
                while (offset + 2 <= header.DataOffset)
                {
                    ushort len = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
                    if (offset + 2 + len > header.DataOffset) break;

                    string val = Encoding.UTF8.GetString(span.Slice(offset + 2, len));
                    uint id = _nextId++;
                    _stringToId[val] = id;
                    _idToString[id] = val;
                    
                    offset += 2 + len;
                }

                _currentPageId = current;
                _currentOffset = offset;
                current = header.NextPageId;
            }
        }

        public uint GetOrAdd(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (_stringToId.TryGetValue(value, out uint id)) return id;

            id = _nextId++;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > ushort.MaxValue) throw new ArgumentException("String too long");

            int entrySize = 2 + bytes.Length;

            if (_currentOffset + entrySize > BinarySpec.PageSize)
            {
                uint newPageId = _pageManager.AllocatePage(BinarySpec.PageType.StringTable);
                
                // Link current page to new page
                var currentSpan = _pageManager.GetPageSpan(_currentPageId);
                ref var currentHeader = ref MemoryMarshal.AsRef<BinarySpec.PageHeader>(currentSpan);
                currentHeader.NextPageId = newPageId;
                
                // Initialize new page
                var newSpan = _pageManager.GetPageSpan(newPageId);
                var newHeader = new BinarySpec.PageHeader
                {
                    Type = BinarySpec.PageType.StringTable,
                    NextPageId = 0,
                    DataOffset = (ushort)Marshal.SizeOf<BinarySpec.PageHeader>()
                };
                MemoryMarshal.Write(newSpan, in newHeader);

                _currentPageId = newPageId;
                _currentOffset = Marshal.SizeOf<BinarySpec.PageHeader>();
            }

            var span = _pageManager.GetPageSpan(_currentPageId);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(_currentOffset, 2), (ushort)bytes.Length);
            bytes.CopyTo(span.Slice(_currentOffset + 2, bytes.Length));

            _currentOffset += entrySize;
            
            // Update header
            ref var header = ref MemoryMarshal.AsRef<BinarySpec.PageHeader>(span);
            header.DataOffset = (ushort)_currentOffset;

            _stringToId[value] = id;
            _idToString[id] = value;
            return id;
        }

        public IEnumerable<uint> Search(Regex regex)
        {
            foreach (var kvp in _stringToId)
            {
                if (regex.IsMatch(kvp.Key))
                {
                    yield return kvp.Value;
                }
            }
        }

        public string? GetString(uint id) => _idToString.TryGetValue(id, out string? val) ? val : null;
    }
}

