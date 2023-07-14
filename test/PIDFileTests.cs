namespace PIDFile;

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public class PIDFileTests {
    static readonly int PID = Process.GetCurrentProcess().Id;

    static string MakeName([CallerMemberName] string testCase = "")
        => Path.Join(Path.GetTempPath(), Invariant($"{PID}-{testCase}"));

    [Fact]
    public async Task CanOnlyCreateOne() {
        string fileName = MakeName();

        using var _ = await PIDFile.Create(fileName);
        var ex = await Assert.ThrowsAsync<IOException>(() => PIDFile.Create(fileName));
        Assert.True(ex.IsFileAlreadyExists(), $"IsFileAlreadyExists: 0x{ex.HResult:X8}");
    }

    [Fact]
    public async Task PIDIsCorrect() {
        string fileName = MakeName();

        using var pidFile = await PIDFile.Create(fileName);
        Assert.Equal(PID, pidFile.PID);
    }

    [Fact]
    public async Task Owned() {
        string fileName = MakeName();

        using var pidFile = await PIDFile.Create(fileName);
        Assert.True(pidFile.Owned);
    }

    [Fact]
    public async Task OnlyFirstIsOwned() {
        string fileName = MakeName();

        using var _ = await PIDFile.Create(fileName);
        using var existing = await PIDFile.OpenOrCreate(fileName);
        Assert.False(existing.Owned);
    }
}