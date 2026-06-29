using System.Windows;
using NoiseReduction.Core.Logging;
using NoiseReduction.Infrastructure.Pipeline;

namespace NoiseReduction.App.Views;

public partial class AppIdDialog : Window
{
    /// <summary>true if user successfully verified a new AppID; false/not-verified otherwise.</summary>
    public bool WasVerified { get; private set; }
    /// <summary>The AppID that was verified (only valid when WasVerified is true).</summary>
    public string VerifiedAppId { get; private set; } = "";

    public AppIdDialog(string? currentAppId)
    {
        InitializeComponent();
        AppIdTextBox.Text = currentAppId ?? "";
    }

    private async void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        var appId = AppIdTextBox.Text.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            ShowResult(false, "请输入 AppID");
            return;
        }

        VerifyButton.IsEnabled = false;
        LoadingText.Visibility = Visibility.Visible;
        ResultBorder.Visibility = Visibility.Collapsed;

        try
        {
            var (isValid, sdkVersion, errorMessage) = await Task.Run(() =>
                AgoraAinsPipelineSession.VerifyAppId(appId));
        
            if (isValid)
            {
                AppLogger.Instance.Info($"AppID 验证成功 (SDK: {sdkVersion})");
                // Verification passed — auto-close and return the AppID
                WasVerified = true;
                VerifiedAppId = appId;
                DialogResult = true;
                Close();
            }
            else
            {
                AppLogger.Instance.Error($"AppID 验证失败 (SDK: {sdkVersion}): {errorMessage}");
                ShowResult(false,
                    "✗ 验证失败",
                    $"SDK 版本: {sdkVersion}",
                    errorMessage);
                WasVerified = false;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Error(ex, "AppID 验证过程发生异常");
            ShowResult(false, "验证过程出错", "", ex.Message);
        }
        finally
        {
            VerifyButton.IsEnabled = true;
            LoadingText.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowResult(bool success, string title, string? sdkVersion = null, string? detail = null)
    {
        ResultBorder.Visibility = Visibility.Visible;
        ResultBorder.BorderBrush = success
            ? System.Windows.Media.Brushes.Green
            : System.Windows.Media.Brushes.Red;
        ResultBorder.Background = success
            ? (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xFF, 0xF4))
            : (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xF5, 0xF5));

        ResultTitle.Text = title;
        ResultTitle.Foreground = success
            ? System.Windows.Media.Brushes.Green
            : System.Windows.Media.Brushes.Red;

        SdkVersionText.Text = sdkVersion ?? "";
        SdkVersionText.Visibility = string.IsNullOrEmpty(sdkVersion) ? Visibility.Collapsed : Visibility.Visible;

        DetailText.Text = detail ?? "";
        DetailText.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnUnlinkClick(object sender, RoutedEventArgs e)
    {
        WasVerified = true;
        VerifiedAppId = "";  // signals "clear the AppID"
        DialogResult = true;
        Close();
    }

}


