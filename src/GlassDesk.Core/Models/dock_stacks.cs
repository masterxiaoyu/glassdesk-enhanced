namespace GlassDesk.Core.Models;

/// <summary>收纳只保存应用引用；拖放与解散不改动应用库。</summary>
public static class DockStacks
{
  public const string Prefix = "stack:";

  public static DockToolGroup? Find(DockLayout layout, string id)
    => layout.ToolGroups.Find(group => Prefix + group.Id == id);

  public static bool Combine(DockLayout layout, string source, string target)
  {
    if (source == target || !layout.PinnedItemIds.Contains(target))
      return false;
    var sourceGroup = Find(layout, source);
    var targetGroup = Find(layout, target);
    var members = sourceGroup?.ItemIds ?? [source];
    if (targetGroup is null)
    {
      targetGroup = new DockToolGroup
      {
        Id = Guid.NewGuid().ToString("N"), Name = "收纳", ItemIds = [target],
      };
      layout.ToolGroups.Add(targetGroup);
      var index = layout.PinnedItemIds.IndexOf(target);
      layout.PinnedItemIds[index] = Prefix + targetGroup.Id;
    }
    foreach (var id in members)
    {
      if (!targetGroup.ItemIds.Contains(id)) targetGroup.ItemIds.Add(id);
      layout.PinnedItemIds.Remove(id);
    }
    layout.PinnedItemIds.Remove(source);
    if (sourceGroup is not null) layout.ToolGroups.Remove(sourceGroup);
    return true;
  }

  public static void Dissolve(DockLayout layout, DockToolGroup group)
  {
    var index = layout.PinnedItemIds.IndexOf(Prefix + group.Id);
    if (index >= 0)
    {
      layout.PinnedItemIds.RemoveAt(index);
      layout.PinnedItemIds.InsertRange(index, group.ItemIds
        .Where(id => !layout.PinnedItemIds.Contains(id)));
    }
    layout.ToolGroups.Remove(group);
  }
}
