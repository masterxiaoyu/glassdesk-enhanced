using System.Windows;
using System.Windows.Controls;
using GlassDesk.AppServices;
using GlassDesk.Core.Contracts;
using GlassDesk.Core.Models;
using GlassDesk.Core.Storage;
using Microsoft.Win32;

namespace GlassDesk.Dock;

/// <summary>选择应用库中的软件，编辑一组 Dock 工具栏入口。</summary>
public partial class DockToolGroupEditor : Window
{
  private readonly AppServiceHost _services;

  public DockToolGroupEditor(AppServiceHost services,
    DockToolGroup? group = null)
  {
    InitializeComponent();
    _services = services;
    Title = group is null ? "新建软件聚合" : "编辑软件聚合";
    GroupNameInput.Text = group?.Name ?? string.Empty;
    LoadChoices(group?.ItemIds ?? []);
    Loaded += (_, _) => _ = GroupNameInput.Focus();
  }

  public string GroupNameValue { get; private set; } = string.Empty;

  public List<string> SelectedItemIds { get; private set; } = [];

  private void LoadChoices(IReadOnlyCollection<string> selectedIds)
  {
    var selected = selectedIds.ToHashSet(StringComparer.Ordinal);
    AppChoices.Children.Clear();

    var entries = _services.Library.Items
      .Where(entry => entry.IsUsable &&
        entry.Kind is AppItemKind.Program or AppItemKind.Shortcut)
      .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCulture)
      .ToList();

    if (entries.Count == 0)
    {
      _ = AppChoices.Children.Add(new TextBlock
      {
        Text = "应用库里还没有软件。点击“导入软件”添加。",
        Margin = new Thickness(4, 8, 4, 8),
        TextWrapping = TextWrapping.Wrap,
      });
    }

    foreach (var entry in entries)
    {
      _ = AppChoices.Children.Add(new CheckBox
      {
        Content = entry.DisplayName,
        Tag = entry.Id,
        ToolTip = entry.Target,
        IsChecked = selected.Contains(entry.Id),
        Margin = new Thickness(4, 5, 4, 5),
      });
    }
  }

  private void OnImportClick(object sender, RoutedEventArgs e)
  {
    var picker = new OpenFileDialog
    {
      Title = "选择要加入聚合的软件",
      Multiselect = true,
      DereferenceLinks = false,
      Filter = "程序或快捷方式|*.exe;*.lnk",
    };

    if (picker.ShowDialog(this) != true)
    {
      return;
    }

    var selected = AppChoices.Children.OfType<CheckBox>()
      .Where(choice => choice.IsChecked == true)
      .Select(choice => (string)choice.Tag)
      .ToHashSet(StringComparer.Ordinal);

    try
    {
      // 导入服务先将文件收进共用应用库；聚合只保存稳定的应用 id。
      var requests = picker.FileNames
        .Select(path => new ImportRequest { Source = path })
        .ToList();
      var result = _services.Import.ImportItems(requests);
      selected.UnionWith(result.Added.Concat(result.Duplicates)
        .Select(entry => entry.Id));
      LoadChoices(selected);

      if (result.Failures.Count > 0)
      {
        MessageBox.Show(this,
          string.Join(Environment.NewLine, result.Failures
            .Select(failure => failure.Source + "："
              + failure.Message)),
          "部分软件未能导入", MessageBoxButton.OK,
          MessageBoxImage.Warning);
      }
    }
    catch (StorageException ex)
    {
      MessageBox.Show(this, ex.Message, "导入软件失败",
        MessageBoxButton.OK, MessageBoxImage.Warning);
    }
  }

  private void OnSaveClick(object sender, RoutedEventArgs e)
  {
    var name = GroupNameInput.Text.Trim();
    var items = AppChoices.Children.OfType<CheckBox>()
      .Where(choice => choice.IsChecked == true)
      .Select(choice => (string)choice.Tag)
      .ToList();

    if (name.Length == 0 || items.Count == 0)
    {
      MessageBox.Show(this, "请填写名称，并至少选择一个软件。", "软件聚合",
        MessageBoxButton.OK, MessageBoxImage.Information);
      return;
    }

    GroupNameValue = name;
    SelectedItemIds = items;
    DialogResult = true;
  }
}
