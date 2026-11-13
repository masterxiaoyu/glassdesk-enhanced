using GlassDesk.Core.Models.Settings;

namespace GlassDesk.Core.Tests;

public sealed class TaskbarModeTests
{
  [Theory]
  [InlineData(false, false)]
  [InlineData(false, true)]
  [InlineData(true, false)]
  [InlineData(true, true)]
  public void 旧任务栏开关组合按所选模式归一(bool topBar, bool hidden)
  {
    var settings = new GlassDeskSettings
    {
      Dock = new DockSettings { ShowTopBar = topBar, HideNativeTaskbar = hidden },
    };
    settings.Normalize();
    Assert.Equal(topBar, settings.Dock.ShowTopBar);
    Assert.Equal(topBar, settings.Dock.HideNativeTaskbar);
  }
}
