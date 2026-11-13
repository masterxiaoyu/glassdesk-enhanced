using GlassDesk.Platform.Input;

namespace GlassDesk.Dock.Tests;

public sealed class WinKeyGestureTests
{
  [Theory]
  [InlineData(0x5Bu)]
  [InlineData(0x5Cu)]
  public void 单击左右Win只在松开时触发(uint win)
  {
    var gesture = new WinKeyGesture();
    Assert.Equal(WinKeyDecision.Suppress, gesture.Process(win, false));
    Assert.Equal(WinKeyDecision.Suppress, gesture.Process(win, false));
    Assert.Equal(WinKeyDecision.SwitchWindows, gesture.Process(win, true));
    Assert.Equal(0, gesture.ForwardedWindowsKeys);
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(win, true));
  }

  [Theory]
  [InlineData(0x44u)] // Win+D
  [InlineData(0x45u)] // Win+E
  [InlineData(0x09u)] // Win+Tab
  [InlineData(0x20u)] // Win+Space
  public void 组合键重放前缀且松开不触发切换(uint key)
  {
    var gesture = new WinKeyGesture();
    gesture.Process(0x5B, false);
    Assert.Equal(WinKeyDecision.ReplayChord, gesture.Process(key, false));
    Assert.Equal(1, gesture.ForwardedWindowsKeys);
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(key, false));
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(key, true));
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(0x5B, true));
    gesture.Process(0x5B, false);
    Assert.Equal(WinKeyDecision.SwitchWindows, gesture.Process(0x5B, true));
  }

  [Theory]
  [InlineData(0x10u)]
  [InlineData(0x11u)]
  [InlineData(0x12u)]
  public void 先按修饰键再按Win保持原顺序(uint modifier)
  {
    var gesture = new WinKeyGesture();
    gesture.Process(modifier, false);
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(0x5B, false));
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(0x25, false));
    gesture.Process(0x25, true);
    gesture.Process(modifier, true);
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(0x5B, true));
  }

  [Fact]
  public void 安装监听前已按住修饰键也不作为单击()
  {
    var gesture = new WinKeyGesture();
    Assert.Equal(WinKeyDecision.PassThrough,
      gesture.Process(0x5C, false, modifiersHeld: true));
    Assert.Equal(WinKeyDecision.PassThrough, gesture.Process(0x5C, true));
  }

  [Fact]
  public void 左右Win重叠只在最后松开时触发一次()
  {
    var gesture = new WinKeyGesture();
    gesture.Process(0x5B, false);
    gesture.Process(0x5C, false);
    Assert.Equal(WinKeyDecision.Suppress, gesture.Process(0x5B, true));
    Assert.Equal(WinKeyDecision.SwitchWindows, gesture.Process(0x5C, true));
  }
}
