// AeroDial — TriggerPage.cs
// Split from SettingsPages.cs: one settings page per file.

using AeroDial.Config;
using AeroDial.Core;
using AeroDial.Overlay;
using AeroDial.Themes;
using AeroDial.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace AeroDial.UI.Views.Pages;

// ═══════════════════════════════════════════════════════════════════════════
// TriggerPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class TriggerPage : Page
{
    private TextBlock     _keyDisplay  = null!;
    private Button        _recordBtn   = null!;
    private CheckBox      _ctrl = null!, _alt = null!, _shift = null!;
    private RadioButton   _holdRadio = null!, _toggleRadio = null!;
    private TextBlock     _saved       = null!;
    private bool          _recording;
    private int           _vk;
    private TextBox?      _captureBox;
    private DispatcherTimer? _pollTimer;
    private DispatcherTimer? _saveTimer;

    public TriggerPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(PageKit.PageHeader("Trigger"));

        // Key recorder
        stack.Children.Add(PageKit.SubHeader("Activation button"));
        _vk = App.Config.Current.Trigger.VirtualKey;
        _keyDisplay = new TextBlock
        {
            Text = VkName(_vk), FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Ui.AccentText,
            Margin = new Thickness(0, 0, 0, 6),
        };
        stack.Children.Add(_keyDisplay);

        _recordBtn = new Button { Content = "Record key" };
        _recordBtn.Click += StartRecording;

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        AddQuick(quickRow, "Middle mouse",  0x04);
        AddQuick(quickRow, "Mouse Button 4", 0x05);
        AddQuick(quickRow, "Mouse Button 5", 0x06);

        stack.Children.Add(_recordBtn);
        stack.Children.Add(quickRow);

        // Modifiers
        stack.Children.Add(PageKit.SubHeader("Required modifiers"));
        _ctrl  = new CheckBox { Content = "Ctrl",  IsChecked = App.Config.Current.Trigger.RequireCtrl  };
        _alt   = new CheckBox { Content = "Alt",   IsChecked = App.Config.Current.Trigger.RequireAlt   };
        _shift = new CheckBox { Content = "Shift", IsChecked = App.Config.Current.Trigger.RequireShift };
        var mods = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        mods.Children.Add(_ctrl); mods.Children.Add(_alt); mods.Children.Add(_shift);
        stack.Children.Add(mods);

        // Hold vs toggle
        stack.Children.Add(PageKit.SubHeader("Trigger mode"));
        stack.Children.Add(PageKit.InfoCard(
            "Hold: keep the trigger held, menu stays open while held, release to confirm.\n" +
            "Toggle: tap once to open, tap again to close, select items by clicking or hover dwell."));
        bool isHold = App.Config.Current.Trigger.HoldMode;
        _holdRadio   = new RadioButton { Content = "Hold mode",   GroupName = "TrigMode", IsChecked = isHold  };
        _toggleRadio = new RadioButton { Content = "Toggle mode", GroupName = "TrigMode", IsChecked = !isHold };
        stack.Children.Add(_holdRadio);
        stack.Children.Add(_toggleRadio);

        // Auto-save wiring: any control change schedules a debounced save
        _ctrl.Checked        += (_, _) => ScheduleSave();
        _ctrl.Unchecked      += (_, _) => ScheduleSave();
        _alt.Checked         += (_, _) => ScheduleSave();
        _alt.Unchecked       += (_, _) => ScheduleSave();
        _shift.Checked       += (_, _) => ScheduleSave();
        _shift.Unchecked     += (_, _) => ScheduleSave();
        _holdRadio.Checked   += (_, _) => ScheduleSave();
        _toggleRadio.Checked += (_, _) => ScheduleSave();

        _saved = PageKit.SavedBadge();
        _saved.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack; Content = scroll;
    }

    private void AddQuick(Panel p, string label, int vk)
    {
        var b = new Button { Content = label };
        b.Click += (_, _) => { _vk = vk; _keyDisplay.Text = VkName(vk); ScheduleSave(); };
        p.Children.Add(b);
    }

    private void StartRecording(object s, RoutedEventArgs e)
    {
        if (_recording) return;
        _recording = true;
        _recordBtn.Content = "Waiting...";
        _keyDisplay.Text   = "Press any key or button";

        // Invisible TextBox to capture keyboard input
        _captureBox = new TextBox { Opacity = 0, Width = 1, Height = 1, IsTabStop = true };
        _captureBox.KeyDown += (_, args) =>
        {
            int vk = (int)args.Key;
            if (vk is 0x10 or 0x11 or 0x12) return; // ignore bare modifier keys
            FinishRecording(vk);
            args.Handled = true;
        };
        if (Content is ScrollViewer sv && sv.Content is Panel pan)
            pan.Children.Add(_captureBox);
        _captureBox.Focus(FocusState.Programmatic);

        // Poll for keys that may not fire TextBox.KeyDown reliably.
        // ScrollViewer intercepts navigation keys (Page Up, arrows) for scrolling, so they
        // never reach the TextBox.KeyDown handler — GetAsyncKeyState catches them here instead.
        // Rising-edge detection: only triggers on press, not on hold.
        int[] pollVks =
        {
            0x04, 0x05, 0x06,           // Middle mouse, Mouse Button 4, Mouse Button 5
            0x21, 0x22, 0x23, 0x24,     // Page Up, Page Down, End, Home
            0x25, 0x26, 0x27, 0x28,     // Arrow Left, Up, Right, Down
            0x2C, 0x2D, 0x2E,           // Print Screen, Insert, Delete
        };
        bool[] prev = new bool[pollVks.Length];
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _pollTimer.Tick += (_, _) =>
        {
            if (!_recording) return;
            for (int i = 0; i < pollVks.Length; i++)
            {
                bool pressed = (AeroDial.Core.Win32.GetAsyncKeyState(pollVks[i]) & 0x8000) != 0;
                if (pressed && !prev[i]) { FinishRecording(pollVks[i]); return; }
                prev[i] = pressed;
            }
        };
        _pollTimer.Start();
    }

    private void FinishRecording(int vk)
    {
        _pollTimer?.Stop();
        _pollTimer = null;
        _vk = vk;
        _keyDisplay.Text   = VkName(vk);
        _recordBtn.Content = "Record key";
        _recording         = false;
        if (_captureBox?.Parent is Panel pp) pp.Children.Remove(_captureBox);
        _captureBox = null;
        ScheduleSave();
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        await App.Config.UpdateAsync(cfg =>
        {
            cfg.Trigger.VirtualKey   = _vk;
            cfg.Trigger.RequireCtrl  = _ctrl.IsChecked  == true;
            cfg.Trigger.RequireAlt   = _alt.IsChecked   == true;
            cfg.Trigger.RequireShift = _shift.IsChecked == true;
            cfg.Trigger.HoldMode     = _holdRadio.IsChecked == true;
        });
        App.Hooks.Stop(); App.Hooks.Start();
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? s, object e)
    {
        _saveTimer!.Stop();
        Save(null!, null!);
    }

    private static string VkName(int vk) => vk switch
    {
        0x01 => "Left mouse",     0x02 => "Right mouse",
        0x04 => "Middle mouse",      0x05 => "Mouse Button 4",
        0x06 => "Mouse Button 5",      0x08 => "Backspace",
        0x09 => "Tab",            0x0D => "Enter",
        0x1B => "Escape",         0x20 => "Space",
        // Navigation cluster
        0x21 => "Page Up",        0x22 => "Page Down",
        0x23 => "End",            0x24 => "Home",
        0x25 => "Left",           0x26 => "Up",
        0x27 => "Right",          0x28 => "Down",
        0x2C => "Print Screen",   0x2D => "Insert",
        0x2E => "Delete",
        // Numpad
        0x60 => "Numpad 0",  0x61 => "Numpad 1",  0x62 => "Numpad 2",
        0x63 => "Numpad 3",  0x64 => "Numpad 4",  0x65 => "Numpad 5",
        0x66 => "Numpad 6",  0x67 => "Numpad 7",  0x68 => "Numpad 8",
        0x69 => "Numpad 9",  0x6A => "Num *",     0x6B => "Num +",
        0x6D => "Num -",     0x6E => "Num .",      0x6F => "Num /",
        // Function keys
        0x70 => "F1",  0x71 => "F2",  0x72 => "F3",  0x73 => "F4",
        0x74 => "F5",  0x75 => "F6",  0x76 => "F7",  0x77 => "F8",
        0x78 => "F9",  0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        0x7C => "F13", 0x7D => "F14", 0x7E => "F15", 0x7F => "F16",
        // OEM punctuation (US layout)
        0xBA => ";",    0xBB => "=",   0xBC => ",",   0xBD => "-",
        0xBE => ".",    0xBF => "/",   0xC0 => "`",
        0xDB => "[",    0xDC => "\\",  0xDD => "]",   0xDE => "'",
        // Digits and letters
        >= 0x30 and <= 0x39 => $"{(char)vk}",
        >= 0x41 and <= 0x5A => $"{(char)vk}",
        _ => $"0x{vk:X2}",
    };
}
