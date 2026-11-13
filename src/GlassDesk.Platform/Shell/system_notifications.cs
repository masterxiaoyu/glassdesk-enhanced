using System.Runtime.InteropServices;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using Windows.Management.Deployment;

namespace GlassDesk.Platform.Shell;

public sealed record DesktopNotification(uint Id, string Application,
  string Title, string Body, DateTimeOffset Created);

public enum NotificationAccess
{
  RequiresInstallation,
  RequiresPermission,
  Allowed,
  Unavailable,
}

public sealed record NotificationSnapshot(NotificationAccess Access,
  IReadOnlyList<DesktopNotification> Items, string Status);

public sealed record NotificationInstallResult(bool Succeeded, string Message);

public static class SystemNotifications
{
  public static bool HasIdentity
  {
    get
    {
      uint length = 0;
      return GetCurrentPackageFullName(ref length, 0) == 122;
    }
  }

  public static async Task<NotificationSnapshot> ReadAsync(
    bool requestAccess = false)
  {
    if (!HasIdentity)
      return new(NotificationAccess.RequiresInstallation, [],
        "安装通知组件以同步系统通知。");
    try
    {
      var listener = UserNotificationListener.Current;
      var access = requestAccess ? await listener.RequestAccessAsync()
        : listener.GetAccessStatus();
      if (access != UserNotificationListenerAccessStatus.Allowed)
        return new(NotificationAccess.RequiresPermission, [],
          "需要通知访问权限。");
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var notifications = await listener.GetNotificationsAsync(
        NotificationKinds.Toast).AsTask(timeout.Token);
      var items = notifications.OrderByDescending(item => item.CreationTime)
        .Select(item =>
        {
          var binding = item.Notification.Visual.GetBinding(
            KnownNotificationBindings.ToastGeneric)
            ?? item.Notification.Visual.Bindings.FirstOrDefault();
          var text = binding?.GetTextElements()
            .Select(element => element.Text).ToArray() ?? [];
          return new DesktopNotification(item.Id,
            item.AppInfo.DisplayInfo.DisplayName,
            text.FirstOrDefault() ?? "通知",
            string.Join("\n", text.Skip(1)), item.CreationTime);
        }).ToArray();
      return new(NotificationAccess.Allowed, items,
        items.Length == 0 ? "暂无通知" : "");
    }
    catch (Exception error) when (error is COMException
      or UnauthorizedAccessException or OperationCanceledException)
    {
      return new(NotificationAccess.Unavailable, [],
        error is OperationCanceledException ? "通知同步超时，请重试。"
          : "无法访问系统通知，请检查通知访问权限。");
    }
  }

  public static void Dismiss(uint id)
    => UserNotificationListener.Current.RemoveNotification(id);

  public static async Task<NotificationInstallResult> InstallAsync(
    string packagePath, string applicationDirectory)
  {
    var options = new AddPackageOptions
    {
      ExternalLocationUri = new Uri(applicationDirectory),
    };
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var result = await new PackageManager().AddPackageByUriAsync(
      new Uri(packagePath), options).AsTask(timeout.Token);
    if (result.ExtendedErrorCode?.HResult == unchecked((int)0x800B0109))
      return new(false, "通知组件签名未受信任。请先安装随附的测试证书。");
    return result.ExtendedErrorCode is null
      ? new(true, "通知组件已安装，请重新启动 GlassDesk。")
      : new(false, "通知组件安装失败：" + result.ErrorText);
  }

  public static async Task UninstallAsync()
  {
    var manager = new PackageManager();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var packages = manager.FindPackagesForUser("").Where(package =>
      package.Id.Name == "GlassDesk.Desktop.Preview").ToArray();
    foreach (var package in packages)
    {
      var result = await manager.RemovePackageAsync(package.Id.FullName)
        .AsTask(timeout.Token);
      if (result.ExtendedErrorCode is { } error) throw error;
    }
  }

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
  private static extern int GetCurrentPackageFullName(ref uint length,
    nint name);
}
