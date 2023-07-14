namespace PIDFile;

using System.IO;
using System.Runtime.InteropServices;

static class IOExceptionExtensions {
    const int LINUX_FILE_ALREADY_EXISTS = 17;

    public static bool IsFileAlreadyExists(this IOException error)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? error.HResult == unchecked((int)0x80070050)
            : error.HResult == LINUX_FILE_ALREADY_EXISTS;
}