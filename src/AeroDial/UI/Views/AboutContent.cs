// AeroDial — AboutContent.cs
// Builds the About UI panel used by both Settings > About and the standalone dialog.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AeroDial.UI.Views;

internal static class AboutContent
{
    public static UIElement Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(40, 32, 40, 32) };
        var root   = new StackPanel { Spacing = 0 };

        // ── Icon + name ───────────────────────────────────────────────────
        var logoRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 18,
            Margin      = new Thickness(0, 0, 0, 24),
        };
        logoRow.Children.Add(SettingsWindow.BuildBrandIcon(56));

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text       = "AeroDial",
            FontSize   = 28,
            FontWeight = FontWeights.SemiBold,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text       = $"Version {AppConstants.Version}  |  Windows 10 / 11",
            FontSize   = 13,
            Foreground = Ui.TextSecondary,
        });
        logoRow.Children.Add(titleStack);
        root.Children.Add(logoRow);

        root.Children.Add(Divider(24));

        // ── Description ───────────────────────────────────────────────────
        root.Children.Add(BodyText(
            "AeroDial is a radial launcher overlay for Windows. It opens a " +
            "customisable radial menu wherever your cursor is on the press of any " +
            "key or mouse button, letting you launch apps, trigger key combos, " +
            "control media, run scripts, and more, without touching your taskbar. " +
            "It works on top of any application including games, supports multiple " +
            "monitors at any DPI scale, and is designed to be fast, light, and " +
            "beautiful."));

        root.Children.Add(Spacer(20));

        // ── Update checker ────────────────────────────────────────────────
        root.Children.Add(SectionHeader("Updates"));
        root.Children.Add(Spacer(8));

        var updateText = new TextBlock
        {
            Text         = $"Current version: {AppConstants.Version}",
            FontSize     = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground   = Ui.TextSecondary,
            Margin       = new Thickness(0, 0, 0, 8),
        };
        root.Children.Add(updateText);

        var downloadBtn = new HyperlinkButton
        {
            Content     = "Download latest release",
            NavigateUri = new Uri(AppConstants.GitHubUrl + "/releases/latest"),
            Padding     = new Thickness(0),
            Visibility  = Visibility.Collapsed,
            Margin      = new Thickness(0, 0, 0, 8),
        };
        root.Children.Add(downloadBtn);

        var checkBtn = new Button
        {
            Content = "Check for updates",
            Padding = new Thickness(14, 6, 14, 6),
        };
        checkBtn.Click += async (_, _) =>
        {
            checkBtn.IsEnabled = false;
            checkBtn.Content   = "Checking...";
            downloadBtn.Visibility = Visibility.Collapsed;

            var (status, latest, releaseUrl) = await AeroDial.Core.UpdateChecker.CheckAsync();

            checkBtn.IsEnabled = true;
            checkBtn.Content   = "Check for updates";

            switch (status)
            {
                case AeroDial.Core.UpdateChecker.UpdateStatus.UpdateAvailable:
                    updateText.Text = $"Update available: v{latest}";
                    updateText.Foreground = Ui.Success;
                    if (releaseUrl is not null)
                    {
                        downloadBtn.NavigateUri = new Uri(releaseUrl);
                        downloadBtn.Visibility  = Visibility.Visible;
                    }
                    break;

                case AeroDial.Core.UpdateChecker.UpdateStatus.UpToDate:
                    updateText.Text = $"You are up to date (v{latest}).";
                    updateText.Foreground = Ui.Success;
                    break;

                default:
                    updateText.Text = "Could not reach GitHub. Check your internet connection.";
                    updateText.Foreground = Ui.Caution;
                    break;
            }
        };
        root.Children.Add(checkBtn);
        root.Children.Add(Spacer(24));

        // ── Developer section ─────────────────────────────────────────────
        root.Children.Add(SectionHeader("Developed by"));
        root.Children.Add(Spacer(8));

        var devStack = new StackPanel { Spacing = 4 };
        devStack.Children.Add(new TextBlock
        {
            Text       = "Muhtasim Mahbub",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
        });
        devStack.Children.Add(new TextBlock
        {
            Text       = "3M Design Solutions",
            FontSize   = 13,
            Foreground = Ui.TextSecondary,
        });
        devStack.Children.Add(new HyperlinkButton
        {
            Content     = AppConstants.Website,
            NavigateUri = new Uri(AppConstants.Website),
            Padding     = new Thickness(0),
            Margin      = new Thickness(0, 2, 0, 0),
        });
        var devCard = Ui.Card(devStack, new Thickness(18, 14, 18, 14));
        devCard.Margin = new Thickness(0, 0, 0, 20);
        root.Children.Add(devCard);

        // ── Links ─────────────────────────────────────────────────────────
        root.Children.Add(SectionHeader("Resources"));
        root.Children.Add(Spacer(8));

        var linksRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        linksRow.Children.Add(LinkButton("GitHub",              AppConstants.GitHubUrl));
        linksRow.Children.Add(LinkButton("3M Design Solutions", AppConstants.Website));
        root.Children.Add(linksRow);

        root.Children.Add(Spacer(24));

        // ── License ───────────────────────────────────────────────────────
        root.Children.Add(SectionHeader("License"));
        root.Children.Add(Spacer(8));
        root.Children.Add(BodyText(
            $"AeroDial is open-source software released under the {AppConstants.LicenseName}. " +
            "You are free to use, modify, and distribute it. " +
            "See the LICENSE file in the repository for the full terms."));

        root.Children.Add(Spacer(20));

        // ── Footer ────────────────────────────────────────────────────────
        root.Children.Add(new TextBlock
        {
            Text       = "© 2026 Muhtasim Mahbub | 3M Design Solutions. All rights reserved.",
            FontSize   = 11,
            Foreground = Ui.TextTertiary,
        });

        root.Children.Add(Spacer(28));

        // ── Quit ──────────────────────────────────────────────────────────
        root.Children.Add(Divider(20));
        var quitBtn = new Button
        {
            Content    = "Quit AeroDial",
            Foreground = Ui.Critical,
            Padding    = new Thickness(20, 8, 20, 8),
        };
        quitBtn.Click += (_, _) => App.RequestShutdown();
        root.Children.Add(quitBtn);

        scroll.Content = root;
        return scroll;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string text) => new()
    {
        Text       = text,
        FontSize   = 14,
        FontWeight = FontWeights.SemiBold,
    };

    private static TextBlock BodyText(string text) => new()
    {
        Text         = text,
        FontSize     = 14,
        TextWrapping = TextWrapping.Wrap,
        LineHeight   = 22,
        Foreground   = Ui.TextSecondary,
    };

    private static UIElement Spacer(double h) => new Border { Height = h };

    private static UIElement Divider(double bottomMargin) => new Border
    {
        Height     = 1,
        Background = Ui.Divider,
        Margin     = new Thickness(0, 0, 0, bottomMargin),
    };

    private static HyperlinkButton LinkButton(string label, string url) => new()
    {
        Content     = label,
        NavigateUri = new Uri(url),
    };
}
