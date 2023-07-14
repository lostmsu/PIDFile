namespace PIDFile;

using System.IO;

static class IOExceptionExtensions {
    public static bool IsFileAlreadyExists(this IOException error)
        => error.HResult == unchecked((int)0x80070050);
}