namespace GlassDesk.Platform.Input;

public enum WinKeyDecision
{
  PassThrough,
  Suppress,
  ReplayChord,
  SwitchWindows,
}

/// <summary>区分单独松开 Win 与 Win 组合键，不依赖按住时长。</summary>
public sealed class WinKeyGesture
{
  private readonly HashSet<uint> other_keys_ = [];
  private int windows_keys_;
  private bool chord_;

  public int ForwardedWindowsKeys { get; private set; }

  public WinKeyDecision Process(uint key, bool up, bool modifiersHeld = false)
  {
    var bit = key == 0x5B ? 1 : key == 0x5C ? 2 : 0;
    if (bit == 0)
    {
      if (up) other_keys_.Remove(key);
      else other_keys_.Add(key);
      if (!up && windows_keys_ != 0 && !chord_)
      {
        // 首个组合键连同被暂扣的 Win 一起重放，保证系统收到的顺序。
        chord_ = true;
        ForwardedWindowsKeys = windows_keys_;
        return WinKeyDecision.ReplayChord;
      }
      return WinKeyDecision.PassThrough;
    }
    if (!up)
    {
      if (windows_keys_ == 0)
        chord_ = modifiersHeld || other_keys_.Count != 0;
      windows_keys_ |= bit;
      if (!chord_) return WinKeyDecision.Suppress;
      ForwardedWindowsKeys |= bit;
      return WinKeyDecision.PassThrough;
    }
    if ((windows_keys_ & bit) == 0) return WinKeyDecision.PassThrough;
    windows_keys_ &= ~bit;
    if ((ForwardedWindowsKeys & bit) != 0)
    {
      ForwardedWindowsKeys &= ~bit;
      return WinKeyDecision.PassThrough;
    }
    return windows_keys_ == 0 && !chord_
      ? WinKeyDecision.SwitchWindows : WinKeyDecision.Suppress;
  }
}
