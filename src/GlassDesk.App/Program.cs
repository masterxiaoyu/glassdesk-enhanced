using System.Runtime.CompilerServices;

namespace GlassDesk.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--install-notification-component"
            or "--uninstall-notification-component")
            return RunNotificationComponent(args[0].StartsWith("--uninstall",
                StringComparison.Ordinal));
        // Capture helpers own native resources only. Loading the desktop Application
        // and its theme dictionaries before the pipe handshake delays every cold reveal.
        if (UI.BackdropCaptureWorker.RunIfRequested(args, out var exitCode)) return exitCode;
        return RunDesktop();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunNotificationComponent(bool uninstall)
    {
      // 安装操作独立于桌面宿主，不占用单实例锁，也不修改用户布局。
      try
      {
        if (uninstall)
        {
          Platform.Shell.SystemNotifications.UninstallAsync()
            .GetAwaiter().GetResult();
          return 0;
        }
        var result = Platform.Shell.SystemNotifications.InstallAsync(
          System.IO.Path.Combine(AppContext.BaseDirectory,
            "GlassDesk.Identity.msix"), AppContext.BaseDirectory)
          .GetAwaiter().GetResult();
        if (!result.Succeeded) Console.Error.WriteLine(result.Message);
        return result.Succeeded ? 0 : 1;
      }
      catch (Exception error) when (error is System.Runtime.InteropServices.COMException
        or UnauthorizedAccessException or OperationCanceledException
        or System.IO.IOException)
      {
        Console.Error.WriteLine(error.Message);
        return 1;
      }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunDesktop()
    {
        var application = new App();
        application.InitializeComponent();
        return application.Run();
    }
}
