using System.Windows;
using Point = System.Windows.Point;

namespace NoiseReduction.App.Services;

/// <summary>
/// 进程级窗口位置记忆：主窗口与迷你条各自记住上次拖动结束后的位置。
/// 刻意不持久化到配置文件，生命周期与进程一致。
/// </summary>
public static class WindowPositionStore
{
  /// <summary>主窗口（展开/主窗口模式）上次拖动结束后的位置。</summary>
  public static Point? LastMainWindowPosition { get; set; }

  /// <summary>迷你条窗口上次拖动结束后的位置。</summary>
  public static Point? LastMiniBarPosition { get; set; }

  /// <summary>
  /// 将位置限制在虚拟屏幕范围内，避免显示器数量或布局变化后窗口被恢复到屏幕外。
  /// </summary>
  public static Point ClampToVirtualScreen(Point position, double width, double height)
  {
    double screenLeft = SystemParameters.VirtualScreenLeft;
    double screenTop = SystemParameters.VirtualScreenTop;
    double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
    double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

    // Math.Max 兜底：窗口尺寸大于屏幕时，上下界不会反转为空区间
    double x = Math.Clamp(position.X, screenLeft, Math.Max(screenLeft, screenRight - width));
    double y = Math.Clamp(position.Y, screenTop, Math.Max(screenTop, screenBottom - height));
    return new Point(x, y);
  }
}
