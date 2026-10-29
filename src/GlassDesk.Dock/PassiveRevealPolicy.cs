namespace GlassDesk.Dock;
public static class PassiveRevealPolicy
{
    public static bool CanReveal(bool inHotzone,bool edgeEnabled,bool fullscreen,bool suppressFullscreen,bool covered,bool blockCovered)
        => inHotzone && edgeEnabled && !(fullscreen && suppressFullscreen) && !(covered && blockCovered);

    /// <summary>仅在未被窗口覆盖的全屏状态下，解锁后允许悬停唤出。</summary>
    public static bool CanRevealDuringSuppression(bool pointerOverDock, bool revealLocked)
        => !revealLocked && pointerOverDock;
}
