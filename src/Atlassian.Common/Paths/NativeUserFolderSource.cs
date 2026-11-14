using System.Globalization;
using System.Runtime.InteropServices;

namespace Atlassian.Common.Paths;

/// <summary>
/// Asks the OS for the user's folders. Windows: <c>SHGetKnownFolderPath(FOLDERID_RoamingAppData)</c>
/// with an explicit process token, so the folder belongs to the token's user rather than to the
/// process environment. Linux: <c>getpwuid_r(geteuid())</c>. Nothing here reads an environment variable.
/// </summary>
internal sealed class NativeUserFolderSource : IUserFolderSource
{
    public static readonly NativeUserFolderSource Instance = new();

    private NativeUserFolderSource()
    {
    }

    public UserPlatform Platform =>
        OperatingSystem.IsWindows() ? UserPlatform.Windows
        : OperatingSystem.IsLinux() ? UserPlatform.Linux
        : UserPlatform.Unsupported;

    public UserPathLookup RoamingAppData() =>
        OperatingSystem.IsWindows()
            ? Windows.RoamingAppData()
            : UserPathLookup.Unavailable("the roaming application data folder exists only on Windows");

    public UserPathLookup PasswdHome() =>
        OperatingSystem.IsLinux()
            ? Linux.PasswdHome()
            : UserPathLookup.Unavailable("the passwd home directory is looked up only on Linux");

    private static class Windows
    {
        private const uint TokenQuery = 0x0008;
        private const uint TokenImpersonate = 0x0004;
        private const uint KnownFolderDontVerify = 0x00004000;

        private static readonly Guid RoamingAppDataFolderId = new("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D");

        public static UserPathLookup RoamingAppData()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery | TokenImpersonate, out var token))
            {
                return UserPathLookup.Unavailable(
                    $"OpenProcessToken failed with error {Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}");
            }

            try
            {
                var folderId = RoamingAppDataFolderId;
                var result = SHGetKnownFolderPath(ref folderId, KnownFolderDontVerify, token, out var buffer);
                try
                {
                    if (result != 0)
                    {
                        return UserPathLookup.Unavailable(
                            $"SHGetKnownFolderPath failed with HRESULT 0x{result.ToString("X8", CultureInfo.InvariantCulture)}");
                    }

                    var path = Marshal.PtrToStringUni(buffer);
                    return string.IsNullOrWhiteSpace(path)
                        ? UserPathLookup.Unavailable("SHGetKnownFolderPath returned an empty roaming application data folder")
                        : UserPathLookup.FromPath(path);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
    }

    private static class Linux
    {
        private const int ErrorRange = 34; // ERANGE on Linux
        private const int InitialBufferSize = 1024;
        private const int MaxBufferSize = 1024 * 1024;

        public static UserPathLookup PasswdHome()
        {
            var userId = geteuid();
            for (var size = InitialBufferSize; size <= MaxBufferSize; size *= 2)
            {
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    var error = getpwuid_r(userId, out var entry, buffer, (nuint)size, out var found);
                    if (error == ErrorRange)
                    {
                        continue;
                    }

                    if (error != 0)
                    {
                        return UserPathLookup.Unavailable(
                            $"getpwuid_r failed with error {error.ToString(CultureInfo.InvariantCulture)}");
                    }

                    if (found == IntPtr.Zero)
                    {
                        return UserPathLookup.Unavailable("the effective user id has no passwd entry");
                    }

                    var home = entry.Directory == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(entry.Directory);
                    return string.IsNullOrWhiteSpace(home)
                        ? UserPathLookup.Unavailable("the passwd entry has an empty home directory")
                        : UserPathLookup.FromPath(home);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return UserPathLookup.Unavailable("the passwd entry is too large to read");
        }

        /// <summary>glibc and musl <c>struct passwd</c>.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Passwd
        {
            public IntPtr Name;
            public IntPtr Password;
            public uint UserId;
            public uint GroupId;
            public IntPtr Gecos;
            public IntPtr Directory;
            public IntPtr Shell;
        }

        [DllImport("libc", ExactSpelling = true)]
        private static extern uint geteuid();

        [DllImport("libc", ExactSpelling = true)]
        private static extern int getpwuid_r(uint uid, out Passwd entry, IntPtr buffer, nuint bufferLength, out IntPtr result);
    }
}
