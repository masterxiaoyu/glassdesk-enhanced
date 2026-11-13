using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GlassDesk.Core.Models;
using GlassDesk.Core.Models.Settings;
using GlassDesk.Core.Storage;
using GlassDesk.Platform;
using GlassDesk.UI.Theming;

namespace GlassDesk.Dock;

public partial class DockWindow
{
  private void CombineStack(string source, string target)
  {
    if (_services is null) return;
    if (!DockStacks.Combine(_services.Layouts.Dock, source, target)) return;
    try { _services.Layouts.SaveDock(); }
    catch (StorageException)
    {
      ShowFailureNotice("收纳未能保存，原来的排列已恢复。");
    }
  }

  private DockTileModel CreateStackTile(DockToolGroup group)
  {
    // 用成员的小图标组成预览，区别于普通软件，无需独立的新建入口。
    var drawing = new DrawingGroup();
    using (var context = drawing.Open())
    {
      context.DrawRoundedRectangle(new SolidColorBrush(
        Color.FromArgb(90, 150, 170, 190)), null, new Rect(0, 0, 48, 48), 10, 10);
      for (var index = 0; index < Math.Min(4, group.ItemIds.Count); index++)
      {
        var entry = _services!.Library.Find(group.ItemIds[index]);
        if (entry is null) continue;
        context.DrawImage(LoadTileIcon(entry), new Rect(
          5 + index % 2 * 21, 5 + index / 2 * 21, 17, 17));
      }
    }
    var icon = new DrawingImage(drawing);
    icon.Freeze();
    return new DockTileModel
    {
      EntryId = DockStacks.Prefix + group.Id, DisplayName = group.Name,
      ToolTipText = $"{group.Name}（{group.ItemIds.Count} 个应用）",
      Icon = icon, IconSize = DockSettings.FixedIconSize * DockScale,
      TileMargin = new Thickness(4 * DockScale, 0, 4 * DockScale, 0),
    };
  }

  private void ShowStack(DockToolGroup group, bool editing = false)
  {
    _autoHide?.PushSuppress(DateTime.UtcNow);
    var foreground = WindowControl.GetForegroundWindow();
    nint menuHandle = 0;
    var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
    menu.Opened += (_, _) => menuHandle =
      (PresentationSource.FromVisual(menu) as
        System.Windows.Interop.HwndSource)?.Handle ?? 0;
    if (editing)
    {
      _ = menu.Items.Add(MenuItem("展开到 Dock", () =>
      {
        DockStacks.Dissolve(_services!.Layouts.Dock, group);
        try { _services.Layouts.SaveDock(); }
        catch (StorageException)
        {
          ShowFailureNotice("未能保存，原来的收纳已恢复。");
        }
        RefreshTiles();
      }));
      _ = menu.Items.Add(MenuItem("从 Dock 移除", () =>
        OnUnpinRequested(this, DockStacks.Prefix + group.Id)));
    }
    else
    {
      var items = new WrapPanel { Width = 256 };
      foreach (var id in group.ItemIds)
      {
        var entry = _services!.Library.Find(id);
        if (entry is not { IsUsable: true }) continue;
        var content = new StackPanel();
        content.Children.Add(new Image
        {
          Source = LoadTileIcon(entry), Width = 32, Height = 32,
        });
        content.Children.Add(new TextBlock
        {
          Text = entry.DisplayName,
          TextTrimming = TextTrimming.CharacterEllipsis,
          HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 72,
          Foreground = ThemeService.Current?.Palette.ForegroundBrush,
        });
        var button = new Button
        {
          Content = content, Width = 80, Height = 68,
          ToolTip = entry.DisplayName, Margin = new Thickness(2),
          Background = Brushes.Transparent, BorderThickness = new Thickness(0),
        };
        button.Click += (_, _) =>
        {
          menu.IsOpen = false;
          OpenEntry(id, minimizeIfForeground: false);
        };
        items.Children.Add(button);
      }
      var scroll = new ScrollViewer
      {
        Content = items, MaxHeight = 360,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
      };
      _ = menu.Items.Add(new MenuItem
      {
        Header = scroll, StaysOpenOnClick = true,
      });
    }
    menu.Closed += (_, _) =>
    {
      _autoHide?.PopSuppress(DateTime.UtcNow);
      RestorePopupForeground(foreground, menuHandle);
    };
    _ = WindowActivation.TryBringToForeground(Handle);
    menu.IsOpen = true;
  }
}
