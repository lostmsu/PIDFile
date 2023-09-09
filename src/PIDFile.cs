namespace PIDFile;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class PIDFile: IDisposable {
    readonly FileStream? file;
    public int PID { get; }
    public bool Owned => this.file is not null;

    public static async Task<PIDFile> OpenOrCreate(string path,
                                                   CancellationToken cancel = default) {
        if (path is null) throw new ArgumentNullException(nameof(path));

        while (true) {
            cancel.ThrowIfCancellationRequested();

            bool exists = await Task.Run(() => File.Exists(path), cancel)
                                    .ConfigureAwait(false);
            if (exists) {
                // no need to check for write access on Windows as it can't delete a locked file
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                 || await CanWrite(path, cancel).ConfigureAwait(false)) {
                    exists = !await TryDelete(path, cancel).ConfigureAwait(false);
                }
            }

            if (exists) {
                try {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                    FileShare.ReadWrite | FileShare.Delete,
                                                    bufferSize: 32);
                    switch (await ReadPID(file, cancel).ConfigureAwait(false)) {
                    case { } pid:
                        return new PIDFile(pid);
                    default:
                        continue;
                    }
                } catch (FileNotFoundException) { }
            } else {
                try {
                    return await Create(path, cancel).ConfigureAwait(false);
                } catch (IOException e) when (e.IsFileAlreadyExists()) { }
            }
        }
    }

    public static async Task<PIDFile> Create(string path, CancellationToken cancel = default) {
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                                  bufferSize: 32, FileOptions.DeleteOnClose);
        try {
            int pid = Process.GetCurrentProcess().Id;
            byte[] buffer =
                Encoding.ASCII.GetBytes(pid.ToString(CultureInfo.InvariantCulture));
            await file.WriteAsync(buffer, 0, buffer.Length, cancel)
                      .ConfigureAwait(false);
            await file.FlushAsync(cancel).ConfigureAwait(false);
            var pidFile = new PIDFile(pid, file);
            file = null;
            return pidFile;
        } finally {
            file?.Dispose();
        }
    }

    static async Task<bool> CanWrite(string path, CancellationToken cancel) {
        try {
            await Task
                  .Run(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read)
                           .Close(), cancel)
                  .ConfigureAwait(false);
            return true;
        } catch (FileNotFoundException) {
            return true;
        } catch (IOException) {
            return false;
        }
    }

    static async Task<bool> TryDelete(string path, CancellationToken cancel) {
        try {
            await Task.Run(() => File.Delete(path), cancel).ConfigureAwait(false);
            return true;
        } catch (FileNotFoundException) {
            return true;
        } catch (IOException) {
            // TODO restrict to sharing violation
            return false;
        }
    }

    static async Task<int?> ReadPID(Stream stream, CancellationToken cancel) {
        int result = 0;
        bool found = false;
        bool terminated = false;
        byte[] buffer = new byte[1];
        while (await stream.ReadAsync(buffer, 0, 1, cancel).ConfigureAwait(false) > 0) {
            switch (buffer[0]) {
            case (byte)'\n' or (byte)'\r' or (byte)' ' or (byte)'\t':
                if (found) {
                    terminated = true;
                }
                break;
            case >= (byte)'0' and <= (byte)'9':
                if (terminated) throw new FormatException("Multiple numbers in PID file.");
                found = true;
                result = checked(result * 10 + (buffer[0] - (byte)'0'));
                break;
            default:
                throw new FormatException(
                    Invariant($"Invalid character 0x{buffer[0]:X2} in PID file."));
            }
        }
        return found ? result : null;
    }

    PIDFile(int pid, FileStream? file = null) {
        this.PID = pid;
        this.file = file;
    }

    public void Dispose() {
        this.file?.Dispose();
    }
}