// MIT License

using Alyio.McpMssql.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alyio.McpMssql.Tests.Unit;

public class SnapshotStoreTests : IDisposable
{
    private const string SampleCsv = "id,name\n1,Alice\n";
    private const string SnapshotFileExtension = ".snapshot.csv";
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private readonly string _snapshotsDirectory = Path.Combine(Path.GetTempPath(), $"mcpmssql-snapshots-{Guid.NewGuid():N}");
    private readonly SnapshotStore _store;

    public SnapshotStoreTests()
    {
        _store = new SnapshotStore(_snapshotsDirectory, NullLogger<SnapshotStore>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_snapshotsDirectory))
            {
                Directory.Delete(_snapshotsDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort test cleanup.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Save_Returns_NonEmpty_Id()
    {
        var id = await _store.SaveAsync(SampleCsv, CancellationToken);

        Assert.NotNull(id);
        Assert.NotEmpty(id);
    }

    [Fact]
    public async Task TryGet_Returns_Saved_Csv()
    {
        var id = await _store.SaveAsync(SampleCsv, CancellationToken);

        var result = await _store.TryGetAsync(id, CancellationToken);

        Assert.Equal(SampleCsv, result);
    }

    [Fact]
    public async Task TryGet_Returns_Null_For_Unknown_Id()
    {
        var result = await _store.TryGetAsync("nonexistent", CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Save_Produces_Unique_Ids()
    {
        var id1 = await _store.SaveAsync(SampleCsv, CancellationToken);
        var id2 = await _store.SaveAsync(SampleCsv, CancellationToken);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task Save_Uses_Private_Unix_Permissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var id = await _store.SaveAsync(SampleCsv, CancellationToken);
        var snapshotPath = Path.Combine(_snapshotsDirectory, $"{id}{SnapshotFileExtension}");

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(_snapshotsDirectory));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(snapshotPath));
    }

    [Fact]
    public async Task Save_Deletes_Files_That_Expired_After_Initial_Load()
    {
        var expiredId = await _store.SaveAsync(SampleCsv, CancellationToken);
        var expiredPath = Path.Combine(
            _snapshotsDirectory,
            $"{expiredId}{SnapshotFileExtension}");
        File.SetLastWriteTimeUtc(
            expiredPath,
            DateTime.UtcNow - SnapshotStore.Ttl - TimeSpan.FromDays(1));

        await _store.SaveAsync(SampleCsv, CancellationToken);

        Assert.False(File.Exists(expiredPath));
    }

    [Fact]
    public async Task TryGet_Returns_Null_And_Deletes_Expired_Snapshot_File_On_Initial_Load()
    {
        var id = await _store.SaveAsync(SampleCsv, CancellationToken);
        var snapshotPath = Path.Combine(_snapshotsDirectory, $"{id}{SnapshotFileExtension}");
        File.SetLastWriteTimeUtc(snapshotPath, DateTime.UtcNow - SnapshotStore.Ttl - TimeSpan.FromDays(1));

        var reloaded = new SnapshotStore(_snapshotsDirectory, NullLogger<SnapshotStore>.Instance);
        var result = await reloaded.TryGetAsync(id, CancellationToken);

        Assert.Null(result);
        Assert.False(File.Exists(snapshotPath));
    }

    [Fact]
    public async Task TryGet_Returns_Null_When_Backing_Snapshot_Is_Removed()
    {
        const string id = "deadbeef";
        var snapshotPath = Path.Combine(_snapshotsDirectory, $"{id}{SnapshotFileExtension}");
        Directory.CreateDirectory(_snapshotsDirectory);
        await File.WriteAllTextAsync(snapshotPath, SampleCsv, CancellationToken);

        var reloaded = new SnapshotStore(_snapshotsDirectory, NullLogger<SnapshotStore>.Instance);
        var firstRead = await reloaded.TryGetAsync(id, CancellationToken);
        Assert.Equal(SampleCsv, firstRead);

        File.Delete(snapshotPath);
        var result = await reloaded.TryGetAsync(id, CancellationToken);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("../deadbeef")]
    [InlineData("DEADBEEF")]
    [InlineData("deadbee")]
    [InlineData("deadbeeg")]
    public async Task TryGet_Rejects_Invalid_Ids(string id)
    {
        var result = await _store.TryGetAsync(id, CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGet_Evicts_Expired_Snapshot_From_Current_Instance()
    {
        var id = await _store.SaveAsync(SampleCsv, CancellationToken);
        var snapshotPath = Path.Combine(_snapshotsDirectory, $"{id}{SnapshotFileExtension}");
        File.SetLastWriteTimeUtc(snapshotPath, DateTime.UtcNow - SnapshotStore.Ttl - TimeSpan.FromDays(1));

        var result = await _store.TryGetAsync(id, CancellationToken);

        Assert.Null(result);
        Assert.False(File.Exists(snapshotPath));
    }

    [Fact]
    public async Task TryGet_Evicts_Expired_Existing_File_On_First_Load()
    {
        const string id = "cafebabe";
        var snapshotPath = Path.Combine(_snapshotsDirectory, $"{id}{SnapshotFileExtension}");
        Directory.CreateDirectory(_snapshotsDirectory);
        await File.WriteAllTextAsync(snapshotPath, SampleCsv, CancellationToken);
        File.SetLastWriteTimeUtc(snapshotPath, DateTime.UtcNow - SnapshotStore.Ttl - TimeSpan.FromDays(1));

        var reloaded = new SnapshotStore(_snapshotsDirectory, NullLogger<SnapshotStore>.Instance);

        var result = await reloaded.TryGetAsync(id, CancellationToken);

        Assert.Null(result);
        Assert.False(File.Exists(snapshotPath));
    }
}
