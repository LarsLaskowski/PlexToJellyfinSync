using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PlexToJellyfinSync.Service;

/// <summary>
/// Native calls of the service
/// </summary>
internal static partial class NativeMethods
{
    #region Constants

    // Skeleton: removed with the implementation
#pragma warning disable S1144

    /// <summary>
    /// Directory descriptor that makes a relative path resolve against the working directory
    /// </summary>
    private const int AtFdCwd = -100;

    /// <summary>
    /// Request mask asking the kernel for the file type only
    /// </summary>
    private const uint StatxType = 1;

    /// <summary>
    /// Size of the buffer that receives the <c>statx</c> structure
    /// </summary>
    private const int StatxBufferSize = 256;

    /// <summary>
    /// Offset of the file mode in the <c>statx</c> structure
    /// </summary>
    private const int ModeOffset = 28;

    /// <summary>
    /// Mask of the file type bits in the file mode
    /// </summary>
    private const int FileTypeMask = 0xF000;

    /// <summary>
    /// File type bits of a regular file
    /// </summary>
    private const int RegularFileType = 0x8000;

#pragma warning restore S1144

    #endregion // Constants

    #region Methods

    /// <summary>
    /// Determine whether a path, after following symbolic links, is a regular file
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns><c>true</c> when the path is a regular file</returns>
    /// <exception cref="IOException">The file type could not be read</exception>
    [SupportedOSPlatform("linux")]
    internal static bool IsRegularFile(string path)
    {
        // Skeleton: removed with the implementation
#pragma warning disable RH2003
        throw new NotImplementedException();
#pragma warning restore RH2003
    }

    /// <summary>
    /// Read the file type of a path through <c>statx</c>
    /// </summary>
    /// <param name="directoryDescriptor">Directory descriptor a relative path resolves against</param>
    /// <param name="path">Path to read</param>
    /// <param name="flags">Lookup flags</param>
    /// <param name="mask">Fields requested from the kernel</param>
    /// <param name="buffer">Buffer that receives the <c>statx</c> structure</param>
    /// <returns><c>0</c> on success, <c>-1</c> on failure</returns>
    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Statx(int directoryDescriptor, string path, int flags, uint mask, [Out] byte[] buffer);

    #endregion // Methods
}