using System.Runtime.InteropServices;
using GlassDesk.Platform.Displays;
using GlassDesk.Platform.Interop;

namespace GlassDesk.Platform.Shell;

/// <summary>把顶部栏注册为 AppBar，最大化窗口会避开它。</summary>
public sealed class TopBarReservation : IDisposable
{
  public const int CallbackMessage = 0x8006;
  private readonly nint window_;
  private PixelRect? owned_area_;
  public bool IsReserved { get; private set; }

  public TopBarReservation(nint window) => window_ = window;

  public PixelRect Reserve(DisplayInfo display, int height, bool atTop = false)
  {
    var data = new Shell32.AppBarData
    {
      Size = (uint)Marshal.SizeOf<Shell32.AppBarData>(),
      Window = window_, CallbackMessage = CallbackMessage, Edge = 1,
      Rectangle = new Rect
      {
        Left = display.Bounds.X, Top = display.Bounds.Y,
        Right = display.Bounds.Right, Bottom = display.Bounds.Y + height,
      },
    };
    if (!IsReserved)
      IsReserved = Shell32.SHAppBarMessage(0, ref data) != 0;
    if (IsReserved)
    {
      // 隐藏的原生栏仍可能声明占位；只补足超出的厚度，避免重复避让。
      // 使用 AppBar 保留空间，Explorer 重算时不会覆盖较高的顶部栏。
      Shell32.SHAppBarMessage(2, ref data);
      var reserved_height = atTop
        ? Math.Max(0, height - (data.Rectangle.Top - display.Bounds.Y))
        : height;
      data.Rectangle.Bottom = data.Rectangle.Top + reserved_height;
      var requested = new PixelRect(data.Rectangle.Left, data.Rectangle.Top,
        data.Rectangle.Width, reserved_height);
      // 不重复提交相同布局，防止工作区通知触发连续重排。
      if (owned_area_ != requested)
      {
        Shell32.SHAppBarMessage(3, ref data);
        owned_area_ = new PixelRect(data.Rectangle.Left, data.Rectangle.Top,
          data.Rectangle.Width, data.Rectangle.Height);
      }
    }
    if (atTop) return new PixelRect(display.Bounds.X, display.Bounds.Y,
      display.Bounds.Width, height);
    var rect = data.Rectangle;
    return new PixelRect(rect.Left, rect.Top, rect.Width, height);
  }

  public void Dispose()
  {
    if (!IsReserved) return;
    var data = new Shell32.AppBarData
    {
      Size = (uint)Marshal.SizeOf<Shell32.AppBarData>(), Window = window_,
    };
    Shell32.SHAppBarMessage(1, ref data);
    IsReserved = false;
    owned_area_ = null;
  }
}
