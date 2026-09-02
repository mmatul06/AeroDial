// AeroDial — Pickers.cs
// File and folder pickers for the settings window.
//
// Uses the Windows App SDK pickers (Microsoft.Windows.Storage.Pickers) rather than
// the WinRT Windows.Storage.Pickers ones. The WinRT pickers needed InitializeWithWindow,
// failed when the app ran elevated, and rejected mixed "*" filters with E_INVALIDARG —
// and because the callers were async void, every failure fail-fasted the process
// (0xC000027B). The App SDK pickers take a WindowId directly and work unpackaged.

using AeroDial.Core;
using AeroDial.UI.Views;
using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;

namespace AeroDial.UI;

internal static class Pickers
{
    private static int _open; // 1 while a picker dialog is showing

    /// <summary>Shows an open-file dialog. Returns the chosen path, or null if cancelled or failed.</summary>
    public static async Task<string?> PickFileAsync(params string[] extensions)
    {
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0) return null;
        try
        {
            var picker = new FileOpenPicker(WindowId());
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            // "*" must be the only entry if used; never mix it with specific extensions.
            if (extensions.Length == 0) picker.FileTypeFilter.Add("*");
            else foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);

            var result = await picker.PickSingleFileAsync();
            return string.IsNullOrEmpty(result?.Path) ? null : result!.Path;
        }
        catch (Exception ex)
        {
            Logger.Error("File picker failed", ex);
            App.Tray.ShowBalloon("Couldn't open the file picker", ex.Message);
            return null;
        }
        finally { Interlocked.Exchange(ref _open, 0); }
    }

    /// <summary>Shows a folder picker. Returns the chosen path, or null if cancelled or failed.</summary>
    public static async Task<string?> PickFolderAsync()
    {
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0) return null;
        try
        {
            var picker = new FolderPicker(WindowId());
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            var result = await picker.PickSingleFolderAsync();
            return string.IsNullOrEmpty(result?.Path) ? null : result!.Path;
        }
        catch (Exception ex)
        {
            Logger.Error("Folder picker failed", ex);
            App.Tray.ShowBalloon("Couldn't open the folder picker", ex.Message);
            return null;
        }
        finally { Interlocked.Exchange(ref _open, 0); }
    }

    /// <summary>Shows a save-file dialog. Returns the chosen path, or null if cancelled or failed.</summary>
    public static async Task<string?> PickSaveFileAsync(string suggestedName, string typeLabel, string extension)
    {
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0) return null;
        try
        {
            var picker = new FileSavePicker(WindowId())
            {
                SuggestedFileName      = suggestedName,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeChoices.Add(typeLabel, new List<string> { extension });
            var result = await picker.PickSaveFileAsync();
            return string.IsNullOrEmpty(result?.Path) ? null : result!.Path;
        }
        catch (Exception ex)
        {
            Logger.Error("Save picker failed", ex);
            App.Tray.ShowBalloon("Couldn't open the save dialog", ex.Message);
            return null;
        }
        finally { Interlocked.Exchange(ref _open, 0); }
    }

    private static WindowId WindowId()
        => Win32Interop.GetWindowIdFromWindow(SettingsWindow.WindowHandle);
}
