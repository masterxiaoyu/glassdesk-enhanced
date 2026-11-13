using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GlassDesk.AppServices;
using GlassDesk.Core.Models;
using GlassDesk.Core.Storage;
using GlassDesk.UI.Settings;

namespace GlassDesk.Dock.Tests;

public sealed class DockStacksTests
{
  [Fact]
  public void 收纳磁贴可排序清洗不丢失且移除不删除应用()
  {
    StaTest.Run(() =>
    {
      var root = Path.Combine(Path.GetTempPath(),
        "GlassDesk-stacks-" + Guid.NewGuid().ToString("N"));
      var data = new GlassDeskDataStore(new AppPaths(root));
      data.LoadAll();
      foreach (var id in new[] { "a", "b", "c" })
        data.Library.Add(new AppEntry
        {
          Id = id, DisplayName = id, Kind = AppItemKind.Program,
          Target = Path.Combine(root, id + ".exe"),
        });
      data.Layouts.Dock.PinnedItemIds = ["a", "b", "c"];
      DockStacks.Combine(data.Layouts.Dock, "a", "b");
      data.Layouts.SaveDock();
      var stack = data.Layouts.Dock.PinnedItemIds[0];
      using var services = new AppServiceHost(data);
      var window = new DockWindow();
      try
      {
        var host = DockSettingsHost.Initialize(Dispatcher.CurrentDispatcher);
        window.AttachServices(services, host, () => { });
        window.SanitizePinnedLayout();
        Assert.Equal(2, window.TileCount);
        Assert.Contains(stack, data.Layouts.Dock.PinnedItemIds);
        typeof(DockWindow).GetMethod("SaveDockOrder",
          BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window,
            new object[] { new[] { "c", stack } });
        Assert.Equal(["c", stack], data.Layouts.Dock.PinnedItemIds);
        typeof(DockWindow).GetMethod("OnUnpinRequested",
          BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window,
            [window, stack]);
        Assert.Equal(["c"], data.Layouts.Dock.PinnedItemIds);
        Assert.Empty(data.Layouts.Dock.ToolGroups);
        Assert.Equal(3, data.Library.Items.Count);
      }
      finally
      {
        window.Close();
        Directory.Delete(root, true);
      }
    });
  }

  [Fact]
  public void 叠放仅命中固定应用的中心区域()
  {
    StaTest.Run(() =>
    {
      var strip = new DockTileStrip();
      strip.SetTiles([
        new DockTileModel { EntryId = "a" },
        new DockTileModel { EntryId = "b" },
        new DockTileModel { EntryId = "running:x", WindowHandle = 42 },
      ]);
      var window = new Window { Content = strip, Width = 240, Height = 120 };
      try
      {
        window.Show();
        window.UpdateLayout();
        var positions = strip.PaintedGeometry;
        Assert.Equal("b", strip.StackTargetAt(new Point(positions[1].Center, 20), "a"));
        Assert.Null(strip.StackTargetAt(new Point(positions[0].Center, 20), "a"));
        Assert.Null(strip.StackTargetAt(new Point(positions[2].Center, 20), "a"));
        Assert.Null(strip.StackTargetAt(new Point(positions[1].Center - 23, 20), "a"));
        Assert.Null(strip.StackTargetAt(new Point(positions[1].Center, -5), "a"));

        var saves = 0;
        var stacks = 0;
        var reorder = new DockReorderModel(_ => saves++);
        strip.SetReorderModel(reorder);
        strip.StackRequested += (_, pair) =>
        {
          Assert.Equal(("a", "b"), pair);
          stacks++;
        };
        void SetField(string name, object value) => typeof(DockTileStrip)
          .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
          .SetValue(strip, value);
        SetField("_dragActive", true);
        SetField("_dragStartIndex", 0);
        SetField("stack_target_", "b");
        void Drop()
        {
          var payload = new DataObject("GlassDesk-TileDrag", "a");
          var drop = (DragEventArgs)Activator.CreateInstance(
            typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [payload, DragDropKeyStates.None,
              DragDropEffects.Move, strip, new Point(positions[1].Center, 20)],
            culture: null)!;
          drop.RoutedEvent = DragDrop.DropEvent;
          strip.RaiseEvent(drop);
        }
        reorder.Begin(["a", "b"]);
        SetField("stack_hover_since_", DateTime.UtcNow.AddSeconds(-1));
        Drop();
        Assert.Equal(1, stacks);
        Assert.Equal(0, saves);
        Assert.False(reorder.IsActive);
        // 短暂经过中心仍按排序处理，不能创建收纳。
        reorder.Begin(["a", "b"]);
        SetField("stack_hover_since_", DateTime.UtcNow);
        Drop();
        Assert.Equal(1, stacks);
        Assert.Equal(1, saves);
      }
      finally { window.Close(); }
    });
  }
}
