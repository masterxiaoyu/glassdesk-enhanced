using System.IO;

namespace GlassDesk.Platform.Shell;

public static class TaskbarWindowPolicy
{
  public static bool IsInputExperience(string? path)
  {
    var name = Path.GetFileName(path);
    // 仅排除系统输入宿主，不靠窗口标题猜测，更不结束输入法进程。
    return (string.Equals(name, "TextInputHost.exe",
        StringComparison.OrdinalIgnoreCase)
      || string.Equals(name, "InputApp.exe",
        StringComparison.OrdinalIgnoreCase))
      && path!.StartsWith(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "SystemApps") + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);
  }
}
