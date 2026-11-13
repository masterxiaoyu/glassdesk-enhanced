namespace GlassDesk.Core.Models;

/// <summary>
/// Dock 排列。与应用库分开存储，这样重排
/// Dock 绝不会损坏 <see cref="AppEntry"/>。
/// </summary>
public sealed class DockLayout
{
    /// <summary>
    /// Dock 中显示的应用库条目 id，按顺序排列。库中已不存在的
    /// id 属于过期项，应当跳过而不是当作错误——用户可能已经
    /// 删掉了该应用，Dock 只需相应缩短。
    /// </summary>
    public List<string> PinnedItemIds { get; set; } = [];

    /// <summary>Dock 工具栏中的自定义软件聚合，按显示顺序保存。</summary>
    public List<DockToolGroup> ToolGroups { get; set; } = [];

    /// <summary>顶部栏常驻托盘图标，使用稳定身份并保留拖放顺序。</summary>
    public List<string> TopBarItemIds { get; set; } = [];
}

/// <summary>一组由用户选择的应用库条目，不复制软件路径或图标。</summary>
public sealed class DockToolGroup
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> ItemIds { get; set; } = [];
}
