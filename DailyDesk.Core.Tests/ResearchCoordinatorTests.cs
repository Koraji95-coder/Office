using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

public sealed class ResearchCoordinatorTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ResearchWatchlist MakeWatchlist(
        string id,
        string topic,
        string query,
        bool isEnabled = true
    ) =>
        new()
        {
            Id = id,
            Topic = topic,
            Query = query,
            IsEnabled = isEnabled,
        };

    // -------------------------------------------------------------------------
    // ValidateWatchlistId
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateWatchlistId_Null_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ResearchCoordinator.ValidateWatchlistId(null)
        );
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateWatchlistId_Empty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ResearchCoordinator.ValidateWatchlistId(string.Empty)
        );
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateWatchlistId_Whitespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => ResearchCoordinator.ValidateWatchlistId("   ")
        );
    }

    [Fact]
    public void ValidateWatchlistId_Valid_DoesNotThrow()
    {
        // Should not throw
        ResearchCoordinator.ValidateWatchlistId("some-id");
    }

    // -------------------------------------------------------------------------
    // FindWatchlist
    // -------------------------------------------------------------------------

    [Fact]
    public void FindWatchlist_ExactMatch_ReturnsWatchlist()
    {
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("abc123", "Grounding", "grounding research"),
            MakeWatchlist("def456", "Relays", "relay coordination research"),
        };

        var result = ResearchCoordinator.FindWatchlist(watchlists, "abc123");

        Assert.NotNull(result);
        Assert.Equal("Grounding", result!.Topic);
    }

    [Fact]
    public void FindWatchlist_CaseInsensitiveMatch_ReturnsWatchlist()
    {
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("ABC123", "Grounding", "grounding research"),
        };

        var result = ResearchCoordinator.FindWatchlist(watchlists, "abc123");

        Assert.NotNull(result);
        Assert.Equal("Grounding", result!.Topic);
    }

    [Fact]
    public void FindWatchlist_NotFound_ReturnsNull()
    {
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("abc123", "Grounding", "grounding research"),
        };

        var result = ResearchCoordinator.FindWatchlist(watchlists, "nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void FindWatchlist_EmptyList_ReturnsNull()
    {
        var result = ResearchCoordinator.FindWatchlist([], "any-id");

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // EnsureWatchlistCanRun
    // -------------------------------------------------------------------------

    [Fact]
    public void EnsureWatchlistCanRun_NullWatchlist_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ResearchCoordinator.EnsureWatchlistCanRun(null, "missing-id")
        );
        Assert.Contains("missing-id", ex.Message, StringComparison.Ordinal);
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureWatchlistCanRun_DisabledWatchlist_ThrowsInvalidOperation()
    {
        var watchlist = MakeWatchlist("id1", "Grounding Watch", "grounding", isEnabled: false);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ResearchCoordinator.EnsureWatchlistCanRun(watchlist, "id1")
        );
        Assert.Contains("Grounding Watch", ex.Message, StringComparison.Ordinal);
        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureWatchlistCanRun_EnabledWatchlist_DoesNotThrow()
    {
        var watchlist = MakeWatchlist("id1", "Grounding Watch", "grounding", isEnabled: true);

        // Should not throw
        ResearchCoordinator.EnsureWatchlistCanRun(watchlist, "id1");
    }

    // -------------------------------------------------------------------------
    // UpdateWatchlistLastRunAt
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateWatchlistLastRunAt_UpdatesTargetEntry()
    {
        var runAt = DateTimeOffset.UtcNow;
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("w1", "Topic A", "query A"),
            MakeWatchlist("w2", "Topic B", "query B"),
        };

        var updated = ResearchCoordinator.UpdateWatchlistLastRunAt(watchlists, "w1", runAt);

        var w1 = updated.First(item => item.Id == "w1");
        var w2 = updated.First(item => item.Id == "w2");
        Assert.Equal(runAt, w1.LastRunAt);
        Assert.Null(w2.LastRunAt);
    }

    [Fact]
    public void UpdateWatchlistLastRunAt_CaseInsensitiveIdMatch()
    {
        var runAt = DateTimeOffset.UtcNow;
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("W1", "Topic A", "query A"),
        };

        var updated = ResearchCoordinator.UpdateWatchlistLastRunAt(watchlists, "w1", runAt);

        Assert.Equal(runAt, updated[0].LastRunAt);
    }

    [Fact]
    public void UpdateWatchlistLastRunAt_DoesNotMutateOriginalList()
    {
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("w1", "Topic A", "query A"),
        };

        var original = watchlists[0].LastRunAt;
        ResearchCoordinator.UpdateWatchlistLastRunAt(watchlists, "w1", DateTimeOffset.UtcNow);

        // Original list item should be unchanged
        Assert.Equal(original, watchlists[0].LastRunAt);
    }

    [Fact]
    public void UpdateWatchlistLastRunAt_PreservesAllOtherFields()
    {
        var runAt = DateTimeOffset.UtcNow;
        var watchlists = new List<ResearchWatchlist>
        {
            new()
            {
                Id = "w1",
                Topic = "Protective Relaying",
                Query = "protective relaying standards",
                Frequency = "Daily",
                PreferredPerspective = "EE Mentor",
                SaveToKnowledgeDefault = false,
                IsEnabled = true,
            },
        };

        var updated = ResearchCoordinator.UpdateWatchlistLastRunAt(watchlists, "w1", runAt);

        var w1 = updated[0];
        Assert.Equal("w1", w1.Id);
        Assert.Equal("Protective Relaying", w1.Topic);
        Assert.Equal("protective relaying standards", w1.Query);
        Assert.Equal("Daily", w1.Frequency);
        Assert.Equal("EE Mentor", w1.PreferredPerspective);
        Assert.False(w1.SaveToKnowledgeDefault);
        Assert.True(w1.IsEnabled);
        Assert.Equal(runAt, w1.LastRunAt);
    }

    // -------------------------------------------------------------------------
    // Integration: SaveWatchlistsAsync persists to OperatorMemoryStore
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveWatchlistsAsync_PersistsWatchlistsAndReturnsState()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // Use null db (JSON-file fallback) to avoid LiteDB parallel-init contention
            var memoryStore = new OperatorMemoryStore(tempDir, db: null);
            var coordinator = new ResearchCoordinator(memoryStore);

            var watchlists = new List<ResearchWatchlist>
            {
                MakeWatchlist("w1", "Protection Research", "protective relay query"),
                MakeWatchlist("w2", "Grounding Research", "grounding standards query"),
            };

            var state = await coordinator.SaveWatchlistsAsync(watchlists);

            Assert.Equal(2, state.Watchlists.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadMemoryStateAsync_AfterSave_ContainsSavedWatchlists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // Use null db (JSON-file fallback) to avoid LiteDB parallel-init contention
            var memoryStore = new OperatorMemoryStore(tempDir, db: null);
            var coordinator = new ResearchCoordinator(memoryStore);

            var watchlists = new List<ResearchWatchlist>
            {
                MakeWatchlist("w-persist", "Fault Analysis", "fault analysis query"),
            };

            await coordinator.SaveWatchlistsAsync(watchlists);
            var loaded = await coordinator.LoadMemoryStateAsync();

            Assert.Single(loaded.Watchlists);
            Assert.Equal("Fault Analysis", loaded.Watchlists[0].Topic);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // ResearchWatchlist model logic
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Daily", 1)]
    [InlineData("Twice Weekly", 3)]
    [InlineData("Weekly", 7)]
    [InlineData("Monthly", 7)] // falls through to default (7 days)
    public void ResearchWatchlist_Interval_RespectsFrequency(string frequency, int expectedDays)
    {
        var watchlist = new ResearchWatchlist { Frequency = frequency };
        Assert.Equal(TimeSpan.FromDays(expectedDays), watchlist.Interval);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenNeverRun_IsTrue()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = true,
            LastRunAt = null, // never run
        };

        Assert.True(watchlist.IsDue);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenDisabled_IsFalse()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = false,
            LastRunAt = null,
        };

        Assert.False(watchlist.IsDue);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenRecentlyRun_IsFalse()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = true,
            Frequency = "Weekly",
            LastRunAt = DateTimeOffset.Now.AddDays(-1), // ran yesterday, weekly = not due yet
        };

        Assert.False(watchlist.IsDue);
    }
}
