using System.Runtime.InteropServices;

namespace MediaPlayer.Helpers;

public static class FilePickerService
{
    private const string AppUserModelId = AppVersion.AppUserModelId;

    private const int OfnAllowMultiSelect = 0x00000200;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;

    public static void EnsureInitialized()
    {
        SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
    }

    public static Task<IReadOnlyList<string>> PickVideosAsync(bool multiple = true, string? title = null)
        => PickMediaAsync(multiple, title);

    public static Task<IReadOnlyList<string>> PickMediaAsync(bool multiple = true, string? title = null)
    {
        var dialogTitle = title ?? (multiple
            ? "Select one or more media files"
            : "Open media file");

        return UiDispatcher.InvokeAsync(() =>
        {
            try
            {
                return PickPathsWin32(dialogTitle, multiple);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Win32 file picker failed: {ex}");
                return (IReadOnlyList<string>)[];
            }
        });
    }

    public static Task<string?> PickSubtitleAsync()
    {
        try
        {
            var paths = PickPathsWin32("Load subtitles", multiple: false);
            return Task.FromResult(paths.Count > 0 ? paths[0] : null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Win32 subtitle picker failed: {ex}");
            return Task.FromResult<string?>(null);
        }
    }

    private static IReadOnlyList<string> PickPathsWin32(string title, bool multiple)
    {
        var filter = BuildWin32Filter(title);
        var maxFileChars = multiple ? 65536 : 260;
        var fileBuffer = Marshal.AllocCoTaskMem(maxFileChars * sizeof(char));
        var titleBuffer = Marshal.AllocCoTaskMem(260 * sizeof(char));

        try
        {
            Marshal.WriteInt16(fileBuffer, 0);

            var ofn = new OpenFileName
            {
                StructSize = Marshal.SizeOf<OpenFileName>(),
                Owner = App.WindowHandle,
                Filter = filter,
                FilterIndex = 1,
                File = fileBuffer,
                MaxFile = maxFileChars,
                FileTitle = titleBuffer,
                MaxFileTitle = 260,
                DialogTitle = title,
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist |
                        (multiple ? OfnAllowMultiSelect : 0)
            };

            if (!GetOpenFileName(ref ofn))
                return [];

            var selected = ReadFileSelectionBuffer(fileBuffer, maxFileChars);
            if (string.IsNullOrWhiteSpace(selected))
                return [];

            if (!multiple)
            {
                var path = selected.Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return path is not null && File.Exists(path) && !Directory.Exists(path) ? [path] : [];
            }

            return ParseMultiSelectPaths(selected);
        }
        finally
        {
            Marshal.FreeCoTaskMem(fileBuffer);
            Marshal.FreeCoTaskMem(titleBuffer);
        }
    }

    private static string ReadFileSelectionBuffer(nint buffer, int maxChars)
    {
        var chars = new char[maxChars];
        for (var i = 0; i < maxChars; i++)
        {
            chars[i] = (char)Marshal.ReadInt16(buffer, i * 2);
            if (chars[i] == '\0' && i > 0 && chars[i - 1] == '\0')
                return new string(chars, 0, i - 1);
        }

        return new string(chars).TrimEnd('\0');
    }

    private static List<string> ParseMultiSelectPaths(string selected)
    {
        var parts = selected.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return [];

        if (parts.Length == 1)
        {
            var path = parts[0];
            return File.Exists(path) && !Directory.Exists(path) ? [path] : [];
        }

        if (Path.IsPathRooted(parts[1]))
            return parts.Where(p => File.Exists(p) && !Directory.Exists(p)).ToList();

        var directory = parts[0].EndsWith('\\') ? parts[0] : parts[0] + '\\';
        var paths = new List<string>();
        for (var i = 1; i < parts.Length; i++)
        {
            var path = Path.IsPathRooted(parts[i])
                ? parts[i]
                : Path.Combine(directory, parts[i]);

            if (File.Exists(path) && !Directory.Exists(path))
                paths.Add(path);
        }

        return paths;
    }

    private static string BuildWin32Filter(string label)
        => $"{label}\0*.*\0All files (*.*)\0*.*\0\0";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int StructSize;
        public nint Owner;
        public nint Instance;
        public string Filter;
        public nint CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public nint File;
        public int MaxFile;
        public nint FileTitle;
        public int MaxFileTitle;
        public string InitialDir;
        public string DialogTitle;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public string DefExt;
        public nint CustData;
        public nint Hook;
        public string TemplateName;
        public nint Reserved;
        public int FlagsEx;
    }
}
