// AeroDial — ActionDispatcher.cs
// Executes menu item actions. Each action type is handled by a dedicated
// private method so the logic stays readable and easy to extend.

using System.Diagnostics;
using System.Runtime.InteropServices;
using AeroDial.Config;
using AeroDial.Core;

namespace AeroDial.Actions;

internal sealed class ActionDispatcher
{
    // ── Entry point ───────────────────────────────────────────────────────

    /// <summary>Execute the action defined by a menu item. Fire-and-forget safe.
    /// Anything that goes through the shell (process launch, URL, script) runs on a
    /// threadpool thread: ShellExecuteEx can block for seconds on a cold-start app or a
    /// UAC prompt, and the caller is the overlay's UI thread, which must return at once
    /// so the ring's close animation can play.</summary>
    public void Execute(MenuItemConfig item)
    {
        try
        {
            Logger.Info($"Executing action: {item.ActionType} — '{item.Label}'");

            switch (item.ActionType)
            {
                case ActionType.LaunchApp:      RunOffThread(item, LaunchApp); break;
                case ActionType.OpenUrl:        RunOffThread(item, OpenUrl);   break;
                case ActionType.RunScript:      RunOffThread(item, RunScript); break;
                case ActionType.KeyCombo:       SendKeyCombo(item);  break;
                case ActionType.Media:          SendMedia(item);     break;
                case ActionType.PasteClipboard: PasteClip(item);    break;
                case ActionType.OpenSettings:   OpenSettings();      break;
                case ActionType.FocusWindow:    FocusWindow(item);   break;
                case ActionType.Macro:          RunMacro(item);      break;
                case ActionType.SubMenu:
                case ActionType.None:           /* handled by overlay */ break;
                default:
                    Logger.Warn($"Unhandled action type: {item.ActionType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            ReportFailure(item, ex);
        }
    }

    private static void RunOffThread(MenuItemConfig item, Action<MenuItemConfig> work)
        => _ = Task.Run(() =>
        {
            try { work(item); }
            catch (Exception ex) { ReportFailure(item, ex); }
        });

    private static void ReportFailure(MenuItemConfig item, Exception ex)
    {
        Logger.Error($"Action execution failed for '{item.Label}'", ex);
        App.Tray.ShowBalloon($"Couldn't run \"{item.Label}\"", ex.Message);
    }

    // ── Action implementations ────────────────────────────────────────────

    private static void LaunchApp(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.AppPath)) return;

        var psi = new ProcessStartInfo
        {
            FileName        = item.AppPath,
            Arguments       = item.AppArgs ?? string.Empty,
            UseShellExecute = true,
        };
        Process.Start(psi);
    }

    private static void OpenUrl(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.Url)) return;
        if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp   && uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeMailto && uri.Scheme != Uri.UriSchemeFile))
        {
            App.Tray.ShowBalloon("Invalid URL", $"\"{item.Url}\" is not a valid web address.");
            return;
        }
        Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
    }

    private static void SendKeyCombo(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.KeyCombo)) return;
        if (!SendChord(item.KeyCombo))
            App.Tray.ShowBalloon("Unrecognized key combo",
                $"\"{item.KeyCombo}\" has no keys AeroDial understands.");
    }

    /// <summary>Presses a chord like "Ctrl+S": modifiers down → keys down → all up
    /// (reverse order). Returns false if no recognized keys were found.</summary>
    private static bool SendChord(string chord)
    {
        var parts     = chord.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var keys      = new List<byte>();
        var modifiers = new List<byte>();

        foreach (var part in parts)
        {
            byte vk = TokenToVk(part);
            if (vk == 0) continue;
            if (vk is 0x5B or 0x11 or 0x12 or 0x10) modifiers.Add(vk);
            else                                    keys.Add(vk);
        }

        if (modifiers.Count == 0 && keys.Count == 0) return false;

        // Build input array: press modifiers → press keys → release keys → release modifiers
        var allDown = modifiers.Concat(keys).ToArray();
        var allUp   = allDown.Reverse().ToArray();

        var inputs = new Win32.INPUT[allDown.Length + allUp.Length];
        int i = 0;
        foreach (var vk in allDown) inputs[i++] = MakeKeyInput(vk, false);
        foreach (var vk in allUp)   inputs[i++] = MakeKeyInput(vk, true);

        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        return true;
    }

    /// <summary>Maps a key token ("Ctrl", "Enter", "A", "F5", "5") to a virtual-key code.
    /// Returns 0 for unrecognized tokens.</summary>
    private static byte TokenToVk(string token)
    {
        string t = token.Trim().ToUpperInvariant();
        return t switch
        {
            "WIN"   or "WINDOWS"  => 0x5B,
            "CTRL"  or "CONTROL"  => 0x11,
            "ALT"                 => 0x12,
            "SHIFT"               => 0x10,
            "TAB"                 => 0x09,
            "ENTER" or "RETURN"   => 0x0D,
            "ESC"   or "ESCAPE"   => 0x1B,
            "SPACE"               => 0x20,
            "DEL"   or "DELETE"   => 0x2E,
            "BACKSPACE" or "BKSP" => 0x08,
            "HOME"                => 0x24,
            "END"                 => 0x23,
            "LEFT"                => 0x25,
            "UP"                  => 0x26,
            "RIGHT"               => 0x27,
            "DOWN"                => 0x28,
            _ when t.Length == 1 && char.IsLetterOrDigit(t[0]) => (byte)t[0],
            _ when t.Length >= 2 && t[0] == 'F' && int.TryParse(t.AsSpan(1), out int fn)
                                 && fn is >= 1 and <= 24        => (byte)(0x6F + fn),
            _                     => 0,
        };
    }

    // ── Macros ────────────────────────────────────────────────────────────

    private static void RunMacro(MenuItemConfig item)
    {
        var steps = item.Macro;
        if (steps is null || steps.Count == 0) return;
        // The overlay has already closed and the previously-focused window is restored
        // (OverlayController.Close), so run on a background thread and let input land there.
        var snapshot = steps.ToList(); // guard against the editor mutating the list mid-run
        _ = Task.Run(() => RunMacroSequence(snapshot));
    }

    private static async Task RunMacroSequence(List<MacroStep> steps)
    {
        const int interKeyDelayMs = 8; // small gap so command-line apps don't drop fast input
        var held = new List<byte>();   // keys pressed via KeyDown, released in finally
        try
        {
            foreach (var step in steps)
            {
                switch (step.Type)
                {
                    case MacroStepType.TypeText:
                        await TypeUnicodeAsync(step.Value, interKeyDelayMs);
                        break;

                    case MacroStepType.KeyPress:
                        if (!SendChord(step.Value))
                            Logger.Warn($"Macro KeyPress had no recognized keys: '{step.Value}'");
                        await Task.Delay(interKeyDelayMs);
                        break;

                    case MacroStepType.KeyDown:
                    {
                        byte vk = TokenToVk(step.Value);
                        if (vk != 0) { SendVk(vk, keyUp: false); held.Add(vk); await Task.Delay(interKeyDelayMs); }
                        break;
                    }

                    case MacroStepType.KeyUp:
                    {
                        byte vk = TokenToVk(step.Value);
                        if (vk != 0) { SendVk(vk, keyUp: true); held.Remove(vk); await Task.Delay(interKeyDelayMs); }
                        break;
                    }

                    case MacroStepType.Delay:
                        if (step.DelayMs > 0) await Task.Delay(step.DelayMs);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Macro execution failed", ex);
            App.Tray.ShowBalloon("Macro failed", ex.Message);
        }
        finally
        {
            // Release anything still held so a partial macro can't leave a key stuck down.
            for (int i = held.Count - 1; i >= 0; i--)
                try { SendVk(held[i], keyUp: true); } catch { /* best effort */ }
        }
    }

    private static async Task TypeUnicodeAsync(string text, int perCharDelayMs)
    {
        foreach (char ch in text)
        {
            SendUnicode(ch);
            if (perCharDelayMs > 0) await Task.Delay(perCharDelayMs);
        }
    }

    private static void SendVk(byte vk, bool keyUp)
    {
        var inputs = new[] { MakeKeyInput(vk, keyUp) };
        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    // Types a single character as a Unicode keystroke (layout-independent, unlike VK codes).
    private static void SendUnicode(char ch)
    {
        const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004;
        var inputs = new[]
        {
            MakeUnicodeInput(ch, KEYEVENTF_UNICODE),
            MakeUnicodeInput(ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP),
        };
        Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static Win32.INPUT MakeUnicodeInput(char ch, uint flags) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new Win32.INPUTUNION
        {
            ki = new Win32.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = flags }
        }
    };

    private static Win32.INPUT MakeKeyInput(byte vk, bool keyUp) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new Win32.INPUTUNION
        {
            ki = new Win32.KEYBDINPUT
            {
                wVk     = vk,
                dwFlags = keyUp ? 0x0002u : 0u, // KEYEVENTF_KEYUP
            }
        }
    };

    private static void SendMedia(MenuItemConfig item)
    {
        byte vk = item.MediaAction switch
        {
            MediaActionType.PlayPause   => 0xB3,
            MediaActionType.Next        => 0xB0,
            MediaActionType.Previous    => 0xB1,
            MediaActionType.VolumeUp    => 0xAF,
            MediaActionType.VolumeDown  => 0xAE,
            MediaActionType.Mute        => 0xAD,
            _                           => 0,
        };

        if (vk == 0) return;

        // Media keys require a down/up cycle.
        Win32.keybd_event(vk, 0, 0, 0);
        Win32.keybd_event(vk, 0, 2, 0); // KEYEVENTF_KEYUP = 2
    }

    private static void RunScript(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.ScriptPath)) return;
        if (!File.Exists(item.ScriptPath))
        {
            App.Tray.ShowBalloon("Script not found", item.ScriptPath);
            return;
        }

        var ext = Path.GetExtension(item.ScriptPath).ToLowerInvariant();
        var psi = ext switch
        {
            ".ps1" => new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NonInteractive -File \"{item.ScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
            },
            _ => new ProcessStartInfo
            {
                FileName        = item.ScriptPath,
                UseShellExecute = true,
            }
        };
        Process.Start(psi);
    }

    private static void PasteClip(MenuItemConfig item)
    {
        if (string.IsNullOrWhiteSpace(item.ClipText)) return;

        // Set clipboard and then send Ctrl+V.
        // We dispatch to the UI thread for clipboard access.
        App.Tray.DispatcherQueue.TryEnqueue(() =>
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(item.ClipText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        });

        // Small delay so the clipboard is populated before we paste.
        Task.Delay(80).ContinueWith(_ =>
            SendKeyCombo(new MenuItemConfig { KeyCombo = "Ctrl+V" }));
    }

    private static void FocusWindow(MenuItemConfig item)
    {
        if (item.WindowHandle == 0) return;
        Win32.ShowWindow(item.WindowHandle, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(item.WindowHandle);
    }

    private static void OpenSettings()
        => App.Tray.DispatcherQueue.TryEnqueue(() =>
            AeroDial.UI.Views.SettingsWindow.ShowOrActivate());
}
