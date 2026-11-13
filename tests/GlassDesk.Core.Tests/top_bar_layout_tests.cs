using GlassDesk.Core.Storage;

namespace GlassDesk.Core.Tests;

public sealed class TopBarLayoutTests
{
  [Fact]
  public void 常驻图标重载保留身份与顺序且保存失败回滚()
  {
    using var temp = new TempDataDirectory();
    var store = new LayoutStore(temp.Paths.DockLayoutFile,
      temp.Paths.LauncherLayoutFile);
    store.Dock.TopBarItemIds = ["guid:first", "app:tool.exe:8"];
    store.SaveDock();
    var loaded = new LayoutStore(temp.Paths.DockLayoutFile,
      temp.Paths.LauncherLayoutFile);
    loaded.LoadDock();
    Assert.Equal(store.Dock.TopBarItemIds, loaded.Dock.TopBarItemIds);
    using (var blocker = new FileStream(store.DockFilePath + AtomicFile.TempSuffix,
      FileMode.Create, FileAccess.ReadWrite, FileShare.None))
    {
      store.Dock.TopBarItemIds.Reverse();
      store.Dock.TopBarItemIds.Add("unsaved");
      Assert.Throws<StorageException>(() => store.SaveDock());
    }
    Assert.Equal(["guid:first", "app:tool.exe:8"], store.Dock.TopBarItemIds);
  }
}
