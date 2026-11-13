using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlassDesk.Platform.Shell;

namespace GlassDesk.UI.Controls;

/// <summary>收纳面板与菜单栏共享托盘点击、回调和拖动行为。</summary>
public sealed class TrayIconButton : Button
{
  public const string DragFormat = "GlassDesk-SystemTrayItem";
  private Point press_;
  private int clicks_;
  private bool pressed_;
  private readonly SystemTrayItem item_;
  public SystemTrayItem Item => item_;
  public event EventHandler? RightClicked;
  public event EventHandler? DragStarted;
  public event EventHandler? DragFinished;

  public TrayIconButton(SystemTrayItem item, double height = 28)
  {
    item_ = item;
    SetResourceReference(StyleProperty, typeof(Button));
    Width = 28;
    Height = height;
    Padding = new Thickness(0);
    Margin = new Thickness(2, 0, 2, 0);
    Background = Brushes.Transparent;
    BorderThickness = new Thickness(0);
    HorizontalContentAlignment = HorizontalAlignment.Center;
    VerticalContentAlignment = VerticalAlignment.Center;
    ToolTip = item.Title;
    System.Windows.Automation.AutomationProperties.SetName(this,
      item.Title.Length > 0 ? item.Title : "后台应用图标");
    if (item.Icon is { } icon)
    {
      var bitmap = BitmapSource.Create(icon.Width, icon.Height, 96, 96,
        PixelFormats.Bgra32, null, icon.Bgra, icon.Width * 4);
      bitmap.Freeze();
      Content = new Image
      {
        Source = bitmap, Width = 16, Height = 16, Stretch = Stretch.Uniform,
      };
    }
    else Content = new TextBlock { Text = "●", FontSize = 12 };
  }

  protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
  {
    base.OnPreviewMouseDown(e);
    if (e.ChangedButton == MouseButton.Left)
    {
      press_ = e.GetPosition(this);
      clicks_ = e.ClickCount;
      pressed_ = true;
      CaptureMouse();
    }
    else if (e.ChangedButton == MouseButton.Right)
      Send(0x0204, e.GetPosition(this));
    e.Handled = true;
  }

  protected override void OnPreviewMouseMove(MouseEventArgs e)
  {
    base.OnPreviewMouseMove(e);
    if (!pressed_ || e.LeftButton != MouseButtonState.Pressed) return;
    var point = e.GetPosition(this);
    if (Math.Abs(point.X - press_.X)
          < SystemParameters.MinimumHorizontalDragDistance
        && Math.Abs(point.Y - press_.Y)
          < SystemParameters.MinimumVerticalDragDistance)
      return;
    // 超过拖动距离后不投递点击，避免拖走图标时先打开原应用。
    pressed_ = false;
    DragStarted?.Invoke(this, EventArgs.Empty);
    ReleaseMouseCapture();
    try
    {
      DragDrop.DoDragDrop(this, new DataObject(DragFormat, item_.PersistentId),
        DragDropEffects.Move);
    }
    finally { DragFinished?.Invoke(this, EventArgs.Empty); }
  }

  protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
  {
    base.OnPreviewMouseUp(e);
    var point = e.GetPosition(this);
    var right = e.ChangedButton == MouseButton.Right;
    if (!right && !pressed_) { e.Handled = true; return; }
    pressed_ = false;
    ReleaseMouseCapture();
    if (right)
    {
      Send(0x0205, point);
      if (item_.Version >= 3) Send(0x007B, point);
      RightClicked?.Invoke(this, EventArgs.Empty);
    }
    else OnClick();
    e.Handled = true;
  }

  protected override void OnClick()
  {
    var center = new Point(ActualWidth / 2, ActualHeight / 2);
    Send(clicks_ == 2 ? 0x0203u : 0x0201u, center);
    Send(0x0202, center);
    if (item_.Version >= 3) Send(0x0400, center);
    clicks_ = 1;
    base.OnClick();
  }

  private void Send(uint message, Point point)
  {
    var screen = PointToScreen(point);
    SystemTrayBridge.SendCallback(item_, message, (int)screen.X, (int)screen.Y);
  }
}
