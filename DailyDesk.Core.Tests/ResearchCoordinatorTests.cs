using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests for ResearchCoordinator domain logic — watchlist validation,
/// lookup, mutation helpers, and persistence through OperatorMemoryStore (JSON-file
/// fallback, no LiteDB) so the tests run safely in CI without database contention.
/// </summary>
[Collection("CoordinatorTests")]
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

    [Fact]
    public void ResearchWatchlist_IsDue_WhenOverdue_IsTrue()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = true,
            Frequency = "Daily",
            LastRunAt = DateTimeOffset.Now.AddDays(-3), // ran 3 days ago, daily = overdue
        };

        Assert.True(watchlist.IsDue);
    }

    // -------------------------------------------------------------------------
    // ResearchWatchlist.DueSummary
    // -------------------------------------------------------------------------

    [Fact]
    public void ResearchWatchlist_DueSummary_NeverRun_ReturnsNeverRun()
    {
        var watchlist = new ResearchWatchlist { LastRunAt = null };

        Assert.Equal("never run", watchlist.DueSummary);
    }

    [Fact]
    public void ResearchWatchlist_DueSummary_AfterRun_ContainsFrequencyAndDates()
    {
        var runAt = new DateTimeOffset(2025, 6, 1, 10, 30, 0, TimeSpan.Zero);
        var watchlist = new ResearchWatchlist
        {
            Frequency = "Weekly",
            LastRunAt = runAt,
        };

        var summary = watchlist.DueSummary;

        Assert.Contains("Weekly", summary, StringComparison.Ordinal);
        Assert.Contains("2025-06-01", summary, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // UpdateWatchlistLastRunAt — edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateWatchlistLastRunAt_NoMatchingId_AllEntriesReturnUnchanged()
    {
        var watchlists = new List<ResearchWatchlist>
        {
            MakeWatchlist("w1", "Topic A", "query A"),
            MakeWatchlist("w2", "Topic B", "query B"),
        };

        var updated = ResearchCoordinator.UpdateWatchlistLastRunAt(
            watchlists, "nonexistent", DateTimeOffset.UtcNow
        );

        Assert.All(updated, item => Assert.Null(item.LastRunAt));
    }

    // -------------------------------------------------------------------------
    // Integration: SaveWatchlistsAsync with empty list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveWatchlistsAsync_EmptyList_ReturnsStateWithNoWatchlists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var memoryStore = new OperatorMemoryStore(tempDir, db: null);
            var coordinator = new ResearchCoordinator(memoryStore);

            var state = await coordinator.SaveWatchlistsAsync([]);

            Assert.Empty(state.Watchlists);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Integration: full workflow — save, update LastRunAt, reload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullWorkflow_SaveAndUpdate_ReloadReflectsLastRunAt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var memoryStore = new OperatorMemoryStore(tempDir, db: null);
            var coordinator = new ResearchCoordinator(memoryStore);

            var initial = new List<ResearchWatchlist>
            {
                MakeWatchlist("wf-1", "Transformer Protection", "transformer protection standards"),
                MakeWatchlist("wf-2", "Arc Flash", "arc flash incident energy"),
            };

            // Save initial state
            await coordinator.SaveWatchlistsAsync(initial);

            // Simulate running watchlist wf-1 and updating LastRunAt
            var runAt = DateTimeOffset.UtcNow;
            var loaded = await coordinator.LoadMemoryStateAsync();
            var updated = ResearchCoordinator.UpdateWatchlistLastRunAt(
                loaded.Watchlists, "wf-1", runAt
            );
            await coordinator.SaveWatchlistsAsync(updated);

            // Reload and verify
            var reloaded = await coordinator.LoadMemoryStateAsync();
            var wf1 = reloaded.Watchlists.First(item =>
                item.Id.Equals("wf-1", StringComparison.OrdinalIgnoreCase));
            var wf2 = reloaded.Watchlists.First(item =>
                item.Id.Equals("wf-2", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(wf1.LastRunAt);
            Assert.Null(wf2.LastRunAt);
            Assert.Equal("Transformer Protection", wf1.Topic);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Integration: DueWatchlists reflects only overdue enabled entries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadMemoryStateAsync_DueWatchlists_ContainsOverdueEnabledOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var memoryStore = new OperatorMemoryStore(tempDir, db: null);
            var coordinator = new ResearchCoordinator(memoryStore);

            var watchlists = new List<ResearchWatchlist>
            {
                new()
                {
                    Id = "due-1",
                    Topic = "Overdue Enabled",
                    Query = "query",
                    IsEnabled = true,
                    Frequency = "Daily",
                    LastRunAt = DateTimeOffset.Now.AddDays(-5), // overdue
                },
                new()
                {
                    Id = "not-due-1",
                    Topic = "Recent Enabled",
                    Query = "query",
                    IsEnabled = true,
                    Frequency = "Weekly",
                    LastRunAt = DateTimeOffset.Now.AddHours(-1), // ran recently
                },
                new()
                {
                    Id = "disabled-1",
                    Topic = "Overdue Disabled",
                    Query = "query",
                    IsEnabled = false,
                    Frequency = "Daily",
                    LastRunAt = DateTimeOffset.Now.AddDays(-5), // overdue but disabled
                },
            };

            await coordinator.SaveWatchlistsAsync(watchlists);
            var state = await coordinator.LoadMemoryStateAsync();

            Assert.Single(state.DueWatchlists);
            Assert.Equal("due-1", state.DueWatchlists[0].Id);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Cancellation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveWatchlistsAsync_PreCancelledToken_ThrowsOperationCancelled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"research-coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var memoryStore = new OperatorMemoryStore(tempDir, db: null);
            var coordinator = new ResearchCoordinator(memoryStore);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var watchlists = new List<ResearchWatchlist>
            {
                MakeWatchlist("w1", "Cancel Test", "cancel query"),
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => coordinator.SaveWatchlistsAsync(watchlists, cts.Token)
            );
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
