using Xunit;
using Memux.Core.Database;

namespace Memux.Tests;

public class EnvironmentToolingTests : IDisposable
{
    private readonly string _dbPath;

    public EnvironmentToolingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"memux_env_{Guid.NewGuid()}.db");
    }

    [Fact]
    public void ProgramsRegistry_UpsertAndGet_Works()
    {
        using var db = new MemuxDatabase(_dbPath);
        var created = db.UpsertProgram(
            id: null,
            name: "dark_souls",
            exePath: "C:/Games/DarkSoulsRemastered.exe",
            processName: "DarkSoulsRemastered",
            windowTitlePattern: "DARK SOULS",
            launchArgs: null,
            runAsAdmin: true,
            preferredWidth: 1920,
            preferredHeight: 1080,
            isDefault: true);

        Assert.NotNull(created);
        Assert.Equal("dark_souls", created.Name);
        Assert.True(created.IsDefault);

        var fetched = db.GetDefaultProgram();
        Assert.NotNull(fetched);
        Assert.Equal("dark_souls", fetched!.Name);
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
}


