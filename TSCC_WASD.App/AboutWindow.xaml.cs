using System.Windows;
using TSCC_WASD.App.ViewModels;
using TSCC_WASD.Core;

namespace TSCC_WASD.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        string version = AboutInfo.CurrentVersionText;
        VersionText.Text = L.T($"版本 {version} · {AboutInfo.ChannelText(version, true)}", $"Version {version} · {AboutInfo.ChannelText(version, false)}");
        ManualButton.Tag = AboutInfo.ManualUrl(L.Chinese);
        SigningPolicyButton.Tag = AboutInfo.SigningPolicyUrl;
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url }) MainViewModel.OpenUrl(url);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
