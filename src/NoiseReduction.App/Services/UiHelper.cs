using System.Windows;
using System.Windows.Controls;

namespace NoiseReduction.App.Services;

/// <summary>
/// UI helper utilities for consistent tooltip and styling patterns.
///
/// Usage in XAML:
///   <Button ToolTip="提示文字" ToolTipService.ShowOnDisabled="True" />
///
/// Usage in code-behind:
///   UiHelper.SetToolTip(button, "提示文字", showOnDisabled: true);
/// </summary>
public static class UiHelper
{
    /// <summary>
    /// Attach a tooltip to any FrameworkElement, optionally enabling
    /// tooltip display even when the element is disabled.
    /// </summary>
    public static void SetToolTip(FrameworkElement element, string text, bool showOnDisabled = false)
    {
        ToolTipService.SetToolTip(element, text);
        if (showOnDisabled)
        {
            ToolTipService.SetShowOnDisabled(element, true);
        }
    }
}
