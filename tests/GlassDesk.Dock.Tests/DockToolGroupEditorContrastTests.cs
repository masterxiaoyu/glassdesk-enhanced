using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GlassDesk.AppServices;
using GlassDesk.Core.Models;
using GlassDesk.Core.Models.Settings;
using GlassDesk.Core.Storage;
using GlassDesk.UI.Theming;
using ThemeMode = GlassDesk.Core.Models.ThemeMode;

namespace GlassDesk.Dock.Tests;

public sealed class DockToolGroupEditorContrastTests
{
  [Theory]
  [InlineData(ThemeMode.Dark)]
  [InlineData(ThemeMode.Light)]
  public void 输入列表和按钮在两种主题下都能读清(ThemeMode mode)
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-tool-group-contrast-" + Guid.NewGuid().ToString("N"));
      using var theme = ThemeService.Initialize(Dispatcher.CurrentDispatcher);
      theme.Update(new AppearanceSettings { Theme = mode });

      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      data.Library.Add(new AppEntry
      {
        Id = "editor",
        DisplayName = "示例软件",
        Kind = AppItemKind.Program,
        Target = @"C:\Windows\notepad.exe",
      });
      using var services = new AppServiceHost(data);
      var editor = new DockToolGroupEditor(services);
      editor.Resources.MergedDictionaries.Add(new ResourceDictionary
      {
        Source = new Uri("pack://application:,,,/GlassDesk.UI;component/"
          + "Theming/LiquidGlassTheme.xaml"),
      });

      try
      {
        editor.Show();
        editor.UpdateLayout();

        var input = Assert.IsType<TextBox>(editor.FindName("GroupNameInput"));
        var choices = Assert.IsType<StackPanel>(editor.FindName("AppChoices"));
        var choice = Assert.Single(choices.Children.OfType<CheckBox>());
        var buttons = Descendants<Button>(editor).ToArray();
        var import = Assert.Single(buttons, button =>
          Equals(button.Content, "导入软件…"));
        var save = Assert.Single(buttons, button =>
          Equals(button.Content, "保存"));

        // 按控件实际生效的画刷计算对比度，能抓到白底白字的回归。
        AssertContrast(input.Foreground, BackgroundAt(editor, input),
          "名称输入框");
        AssertContrast(choice.Foreground, BackgroundAt(editor, choice),
          "软件列表");
        AssertContrast(import.Foreground, BackgroundAt(editor, import),
          "导入按钮");
        AssertContrast(save.Foreground, BackgroundAt(editor, save),
          "保存按钮");
      }
      finally
      {
        editor.Close();
        if (Directory.Exists(root)) Directory.Delete(root, true);
      }
    });
  }

  private static IEnumerable<T> Descendants<T>(DependencyObject parent)
    where T : DependencyObject
  {
    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
    {
      var child = VisualTreeHelper.GetChild(parent, i);
      if (child is T item) yield return item;
      foreach (var nested in Descendants<T>(child)) yield return nested;
    }
  }

  private static Color BackgroundAt(Window window,
    FrameworkElement element)
  {
    var chain = new Stack<DependencyObject>();
    for (DependencyObject? current = element;
      current is not null && !ReferenceEquals(current, window);
      current = VisualTreeHelper.GetParent(current))
    {
      chain.Push(current);
    }

    // WPF 的窗口背景不一定在控件的可视父链上，先取窗口画刷。
    var color = window.Background is null ? Colors.White
      : Composite(Sample(window.Background), Colors.White);
    foreach (var node in chain)
    {
      Brush? brush = node switch
      {
        Window ancestorWindow => ancestorWindow.Background,
        // CheckBox 模板没有使用 Background 属性，不能把默认白色算进去。
        Control control when control is not CheckBox =>
          control.Background,
        Border border => border.Background,
        Panel panel => panel.Background,
        _ => null,
      };
      if (brush is not null) color = Composite(Sample(brush), color);
    }
    return color;
  }

  private static Color Sample(Brush brush)
  {
    if (brush is SolidColorBrush solid) return solid.Color;
    var gradient = Assert.IsAssignableFrom<GradientBrush>(brush);
    var stops = gradient.GradientStops.OrderBy(stop => stop.Offset).ToArray();
    var right = Array.FindIndex(stops, stop => stop.Offset >= .5);
    if (right <= 0) return stops[0].Color;
    var left = stops[right - 1];
    var next = stops[right];
    var fraction = (.5 - left.Offset) / (next.Offset - left.Offset);
    return Color.FromArgb(
      Mix(left.Color.A, next.Color.A, fraction),
      Mix(left.Color.R, next.Color.R, fraction),
      Mix(left.Color.G, next.Color.G, fraction),
      Mix(left.Color.B, next.Color.B, fraction));
  }

  private static byte Mix(byte left, byte right, double fraction) =>
    (byte)Math.Round(left + (right - left) * fraction);

  private static Color Composite(Color foreground, Color background)
  {
    var alpha = foreground.A / 255d;
    return Color.FromRgb(
      Mix(background.R, foreground.R, alpha),
      Mix(background.G, foreground.G, alpha),
      Mix(background.B, foreground.B, alpha));
  }

  private static void AssertContrast(Brush foreground, Color background,
    string label)
  {
    var text = Composite(Sample(foreground), background);
    var lighter = Math.Max(Luminance(text), Luminance(background));
    var darker = Math.Min(Luminance(text), Luminance(background));
    var contrast = (lighter + .05) / (darker + .05);
    Assert.True(contrast >= 4.5,
      $"{label}对比度只有 {contrast:0.00}: {text} / {background}");
  }

  private static double Luminance(Color color) =>
    .2126 * Channel(color.R) + .7152 * Channel(color.G)
      + .0722 * Channel(color.B);

  private static double Channel(byte value)
  {
    var normalized = value / 255d;
    return normalized <= .04045
      ? normalized / 12.92
      : Math.Pow((normalized + .055) / 1.055, 2.4);
  }
}
