using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using GlassDesk.Core.Models;
using GlassDesk.Core.Storage;
using GlassDesk.Platform;

namespace GlassDesk.Dock;

public partial class DockWindow
{
  /// <summary>从 Dock 打开聚合工具栏；空栏也保留新建入口。</summary>
  private void OnToolboxButtonClick(object sender, RoutedEventArgs e)
  {
    if (_services is null)
    {
      return;
    }

    _autoHide?.PushSuppress(DateTime.UtcNow);
    var foregroundBefore = WindowControl.GetForegroundWindow();
    nint menuHandle = 0;
    var menu = new ContextMenu
    {
      PlacementTarget = ToolboxButton,
      Placement = PlacementMode.MousePoint,
    };
    menu.Opened += (_, _) => menuHandle =
      (PresentationSource.FromVisual(menu) as
        System.Windows.Interop.HwndSource)?.Handle ?? 0;

    foreach (var group in _services.Layouts.Dock.ToolGroups)
    {
      var groupMenu = new MenuItem { Header = group.Name };
      var appCount = 0;

      foreach (var id in group.ItemIds)
      {
        var entry = _services.Library.Find(id);
        if (entry is not { IsUsable: true })
        {
          continue;
        }

        _ = groupMenu.Items.Add(MenuItem(entry.DisplayName,
          () => OpenEntry(id, minimizeIfForeground: false)));
        appCount++;
      }

      if (appCount == 0)
      {
        _ = groupMenu.Items.Add(new MenuItem
        {
          Header = "暂无可用软件",
          IsEnabled = false,
        });
      }

      _ = groupMenu.Items.Add(new Separator());
      _ = groupMenu.Items.Add(MenuItem("编辑聚合…", () =>
        _ = Dispatcher.InvokeAsync(() => EditToolGroup(group.Id),
          DispatcherPriority.Background)));
      _ = groupMenu.Items.Add(MenuItem("删除聚合", () =>
        _ = Dispatcher.InvokeAsync(() => DeleteToolGroup(group.Id),
          DispatcherPriority.Background)));
      _ = menu.Items.Add(groupMenu);
    }

    if (_services.Layouts.Dock.ToolGroups.Count == 0)
    {
      _ = menu.Items.Add(new MenuItem
      {
        Header = "还没有软件聚合",
        IsEnabled = false,
      });
    }

    _ = menu.Items.Add(new Separator());
    _ = menu.Items.Add(MenuItem("新建聚合…", () =>
      _ = Dispatcher.InvokeAsync(() => EditToolGroup(null),
        DispatcherPriority.Background)));

    menu.Closed += (_, _) =>
    {
      _autoHide?.PopSuppress(DateTime.UtcNow);
      RestorePopupForeground(foregroundBefore, menuHandle);
    };

    _ = WindowActivation.TryBringToForeground(Handle);
    menu.IsOpen = true;
  }

  /// <summary>编辑结果只写应用 id；保存失败由 LayoutStore 恢复原数据。</summary>
  private void EditToolGroup(string? groupId)
  {
    if (_services is null)
    {
      return;
    }

    var groups = _services.Layouts.Dock.ToolGroups;
    var group = groupId is null ? null : groups.FirstOrDefault(item =>
      string.Equals(item.Id, groupId, StringComparison.Ordinal));
    if (groupId is not null && group is null)
    {
      return;
    }

    var editor = new DockToolGroupEditor(_services, group) { Owner = this };
    if (editor.ShowDialog() != true)
    {
      return;
    }

    if (group is null)
    {
      groups.Add(new DockToolGroup
      {
        Id = Guid.NewGuid().ToString("N"),
        Name = editor.GroupNameValue,
        ItemIds = editor.SelectedItemIds,
      });
    }
    else
    {
      group.Name = editor.GroupNameValue;
      group.ItemIds = editor.SelectedItemIds;
    }

    try
    {
      _services.Layouts.SaveDock();
    }
    catch (StorageException)
    {
      ShowFailureNotice("软件聚合未能保存，原配置已恢复。");
    }
  }

  /// <summary>删除的只是聚合引用，应用库中的软件继续保留。</summary>
  private void DeleteToolGroup(string groupId)
  {
    if (_services is null)
    {
      return;
    }

    var groups = _services.Layouts.Dock.ToolGroups;
    if (groups.RemoveAll(group => string.Equals(group.Id, groupId,
      StringComparison.Ordinal)) == 0)
    {
      return;
    }

    try
    {
      _services.Layouts.SaveDock();
    }
    catch (StorageException)
    {
      ShowFailureNotice("未能删除软件聚合，原配置已恢复。");
    }
  }
}
