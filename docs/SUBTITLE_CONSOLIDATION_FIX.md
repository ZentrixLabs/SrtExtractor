EK # Subtitle Consolidation Fix

## Problem

Embedded subtitles with overlapping timestamps display in reverse order (bottom-to-top) because multiple subtitle entries share the same timestamp, causing video players to stack them incorrectly.

Example problematic SRT:
```
1
00:00:07,633 --> 00:00:10,469
Woman, voice-over:

2
00:00:07,633 --> 00:00:10,469
I FEEL THE SUN ON MY FACE.
```

This displays incorrectly in most players.

## Solution

The `ConsolidateDuplicateTimestampsAsync` method in `SubtitleOcrService.cs` needs to:
1. Parse all SRT entries into objects with (Number, Timestamp, Text)
2. Group entries by identical timestamp
3. Merge text from all entries with same timestamp into a single entry
4. Re-number sequentially

**Current implementation (lines 339-408) does NOT group by timestamp.** It just re-parses the file.

**Correct implementation should be:**

```csharp
var entries = new List<SubtitleEntry>();
// Parse all entries...

// Group by timestamp and merge text
var grouped = entries
    .GroupBy(e => e.Timestamp)
    .Select(g => new SubtitleEntry
    {
        Number = g.Min(e => e.Number),
        Timestamp = g.Key,
        Text = g.SelectMany(e => e.Text).Distinct().ToList()
    })
    .OrderBy(e => e.Number)
    .ToList();
```

## Status

- ✅ Added `SubtitleEntry` helper class (line 447-452)
- ❌ Consolidation method still needs proper grouping logic

The consolidation is currently being called but doesn't actually group by timestamp.

