using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aire.Services
{
    internal static class ApiTraceLog
    {
        private const int MaxEntries = 500;
        private static readonly object Gate = new();
        private static readonly List<ApiTraceEntry> Entries = new();
        private static long _nextId = 0;

        public static ApiTraceEntry Record(
            string kind,
            string method,
            string message,
            bool? success = null,
            object? data = null)
        {
            var entry = new ApiTraceEntry
            {
                Id        = Interlocked.Increment(ref _nextId),
                Timestamp  = DateTimeOffset.Now,
                Kind      = kind,
                Method    = method,
                Message   = message,
                Success   = success,
                Data      = data
            };

            lock (Gate)
            {
                Entries.Add(entry);
                if (Entries.Count > MaxEntries)
                    Entries.RemoveRange(0, Entries.Count - MaxEntries);
            }

            return entry;
        }

        public static ApiTraceEntry[] GetSince(long afterId = 0, int limit = 100)
        {
            lock (Gate)
            {
                return Entries
                    .Where(e => e.Id > afterId)
                    .Take(Math.Max(1, limit))
                    .ToArray();
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Entries.Clear();
            }
        }
    }
}
