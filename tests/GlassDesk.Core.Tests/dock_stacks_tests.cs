using GlassDesk.Core.Models;
using GlassDesk.Core.Storage;

namespace GlassDesk.Core.Tests;

public sealed class DockStacksTests
{
  [Fact]
  public void 叠放保留目标位置并支持继续收纳及解散()
  {
    var layout = new DockLayout { PinnedItemIds = ["a", "b", "c", "d"] };
    Assert.True(DockStacks.Combine(layout, "a", "c"));
    var group = Assert.Single(layout.ToolGroups);
    var id = DockStacks.Prefix + group.Id;
    Assert.Equal(["b", id, "d"], layout.PinnedItemIds);
    Assert.Equal(["c", "a"], group.ItemIds);
    Assert.True(DockStacks.Combine(layout, "d", id));
    Assert.True(DockStacks.Combine(layout, "external", id));
    Assert.Equal(["c", "a", "d", "external"], group.ItemIds);
    Assert.False(DockStacks.Combine(layout, id, id));
    DockStacks.Dissolve(layout, group);
    Assert.Empty(layout.ToolGroups);
    Assert.Equal(["b", "c", "a", "d", "external"], layout.PinnedItemIds);
  }

  [Fact]
  public void 两个收纳可合并且成员不重复()
  {
    var layout = new DockLayout { PinnedItemIds = ["a", "b", "c", "d"] };
    DockStacks.Combine(layout, "a", "b");
    DockStacks.Combine(layout, "c", "d");
    var ids = layout.PinnedItemIds.ToArray();
    Assert.True(DockStacks.Combine(layout, ids[0], ids[1]));
    var group = Assert.Single(layout.ToolGroups);
    Assert.Equal(["d", "c", "b", "a"], group.ItemIds);
    DockStacks.Combine(layout, "a", layout.PinnedItemIds[0]);
    Assert.Equal(4, group.ItemIds.Count);
  }

  [Fact]
  public void 叠放后重载保留收纳与顺序且失败恢复完整排列()
  {
    using var temp = new TempDataDirectory();
    var store = new LayoutStore(temp.Paths.DockLayoutFile,
      temp.Paths.LauncherLayoutFile);
    store.Dock.PinnedItemIds = ["a", "b", "c"];
    store.SaveDock();
    using (var blocker = new FileStream(store.DockFilePath + AtomicFile.TempSuffix,
      FileMode.Create, FileAccess.ReadWrite, FileShare.None))
    {
      DockStacks.Combine(store.Dock, "a", "b");
      Assert.Throws<StorageException>(() => store.SaveDock());
    }
    Assert.Equal(["a", "b", "c"], store.Dock.PinnedItemIds);
    Assert.Empty(store.Dock.ToolGroups);
    DockStacks.Combine(store.Dock, "a", "b");
    store.SaveDock();
    var loaded = new LayoutStore(temp.Paths.DockLayoutFile,
      temp.Paths.LauncherLayoutFile);
    loaded.LoadDock();
    Assert.Equal(store.Dock.PinnedItemIds, loaded.Dock.PinnedItemIds);
    Assert.Equal(["b", "a"], Assert.Single(loaded.Dock.ToolGroups).ItemIds);
  }
}
