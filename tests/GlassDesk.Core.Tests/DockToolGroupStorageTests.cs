using GlassDesk.Core.Models;
using GlassDesk.Core.Storage;

namespace GlassDesk.Core.Tests;

public sealed class DockToolGroupStorageTests : IDisposable
{
  private readonly TempDataDirectory temp_ = new();

  public void Dispose() => temp_.Dispose();

  [Fact]
  public void 工具栏聚合保存后保持名称和软件顺序()
  {
    var store = new LayoutStore(temp_.Paths.DockLayoutFile,
      temp_.Paths.LauncherLayoutFile);
    store.Dock.ToolGroups.Add(new DockToolGroup
    {
      Id = "group-1",
      Name = "常用工具",
      ItemIds = ["editor", "terminal"],
    });
    store.SaveDock();

    var reloaded = new LayoutStore(temp_.Paths.DockLayoutFile,
      temp_.Paths.LauncherLayoutFile);
    reloaded.LoadDock();

    var group = Assert.Single(reloaded.Dock.ToolGroups);
    Assert.Equal("group-1", group.Id);
    Assert.Equal("常用工具", group.Name);
    Assert.Equal(["editor", "terminal"], group.ItemIds);
  }

  [Fact]
  public void 从应用库删除软件后清理工具栏引用并保留其他软件()
  {
    var data = temp_.CreateStore();
    data.LoadAll();
    var editor = new AppEntry
    {
      Id = AppLibraryStore.NewId(),
      DisplayName = "编辑器",
      Kind = AppItemKind.Program,
      Target = @"C:\Windows\notepad.exe",
    };
    var terminal = new AppEntry
    {
      Id = AppLibraryStore.NewId(),
      DisplayName = "终端",
      Kind = AppItemKind.Program,
      Target = @"C:\Windows\System32\cmd.exe",
    };
    data.Library.Add(editor);
    data.Library.Add(terminal);
    data.Layouts.Dock.ToolGroups.Add(new DockToolGroup
    {
      Id = "group-1",
      Name = "工具",
      ItemIds = [editor.Id, terminal.Id],
    });
    data.Library.Save();
    data.Layouts.SaveDock();

    Assert.True(data.DeleteFromLibrary(editor.Id));

    var reloaded = temp_.CreateStore();
    reloaded.LoadAll();
    var group = Assert.Single(reloaded.Layouts.Dock.ToolGroups);
    Assert.Equal([terminal.Id], group.ItemIds);
    Assert.Null(reloaded.Library.Find(editor.Id));
    Assert.NotNull(reloaded.Library.Find(terminal.Id));

    // 最后一个成员删除后，空聚合也要从持久化布局中消失。
    Assert.True(reloaded.DeleteFromLibrary(terminal.Id));
    var empty = temp_.CreateStore();
    empty.LoadAll();
    Assert.Empty(empty.Layouts.Dock.ToolGroups);
  }

  [Fact]
  public void 工具栏保存失败时恢复上次持久化的完整内容()
  {
    var store = new LayoutStore(temp_.Paths.DockLayoutFile,
      temp_.Paths.LauncherLayoutFile);
    store.Dock.ToolGroups.Add(new DockToolGroup
    {
      Id = "group-1",
      Name = "原名称",
      ItemIds = ["editor", "terminal"],
    });
    store.SaveDock();

    // 占用原子写入的临时文件，模拟磁盘保存失败。
    using (var blocker = new FileStream(
      store.DockFilePath + AtomicFile.TempSuffix,
      FileMode.Create,
      FileAccess.ReadWrite,
      FileShare.None))
    {
      store.Dock.ToolGroups[0].Name = "未保存名称";
      store.Dock.ToolGroups[0].ItemIds.RemoveAt(0);
      store.Dock.ToolGroups.Add(new DockToolGroup
      {
        Id = "group-2",
        Name = "未保存分组",
        ItemIds = ["browser"],
      });
      Assert.Throws<StorageException>(() => store.SaveDock());
    }

    var group = Assert.Single(store.Dock.ToolGroups);
    Assert.Equal("原名称", group.Name);
    Assert.Equal(["editor", "terminal"], group.ItemIds);

    var reloaded = new LayoutStore(temp_.Paths.DockLayoutFile,
      temp_.Paths.LauncherLayoutFile);
    reloaded.LoadDock();
    Assert.Equal(group.ItemIds, Assert.Single(reloaded.Dock.ToolGroups).ItemIds);
  }
}
