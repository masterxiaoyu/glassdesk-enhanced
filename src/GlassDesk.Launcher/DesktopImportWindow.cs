using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlassDesk.Core.Imaging;
using GlassDesk.Platform.Shell;
using GlassDesk.UI.Controls;

namespace GlassDesk.Launcher;

/// <summary>选择桌面项目；只有确认后调用方才会实际导入。</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF window resources are released in Closed, matching GlassWindow and LauncherWindow.")]
public sealed class DesktopImportWindow : GlassWindow
{
    protected override bool UseOpaquePanel => true;
    protected override double ResolveCornerRadius(
        GlassDesk.Core.Models.Settings.AppearanceSettings appearance) => 18;
    private readonly List<(DesktopItem Item, CheckBox Choice, Image Icon)> _rows = [];
    private readonly TextBlock _selectionCount = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _add = new() { Content = "添加所选", MinWidth = 100, IsDefault = true, IsEnabled = false };
    private LauncherIconLoader? _icons;
    private readonly ListBox _list = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch
    };
    private bool _changingSelection, _closed;

    public IReadOnlyList<string> SelectedPaths => _rows.Where(row => row.Choice.IsChecked == true)
        .Select(row => row.Item.Path).ToArray();

    public DesktopImportWindow(DesktopScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        Title = "从桌面批量添加";
        AutomationProperties.SetName(this, Title);
        Width = 600; Height = 560; MinWidth = 420; MinHeight = 320;
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        NameScope.SetNameScope(this, new NameScope());

        var panel = new DockPanel { Margin = new Thickness(22) };
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 20, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "勾选要添加的桌面文件、程序、快捷方式或文件夹。",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 14), Opacity = .8 });
        var search = new TextBox {
            ToolTip = "按名称或路径搜索桌面项目" };
        AutomationProperties.SetName(search, "搜索桌面项目");
        RegisterName("DesktopSearchBox", search);
        var searchHost = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        var hint = new TextBlock { Text = "搜索桌面项目…", Opacity = .5,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false };
        searchHost.Children.Add(search); searchHost.Children.Add(hint);
        heading.Children.Add(searchHost);
        var selection = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(_selectionCount, Dock.Right);
        selection.Children.Add(_selectionCount);
        var selectActions = new StackPanel { Orientation = Orientation.Horizontal };
        var selectAll = new Button { Content = "全选", MinWidth = 66, Margin = new Thickness(0, 0, 8, 0) };
        var clear = new Button { Content = "取消全选", MinWidth = 82 };
        RegisterName("SelectAllButton", selectAll); RegisterName("ClearSelectionButton", clear);
        selectAll.Click += (_, _) => SelectAll(true);
        clear.Click += (_, _) => SelectAll(false);
        selectActions.Children.Add(selectAll); selectActions.Children.Add(clear);
        selection.Children.Add(selectActions); heading.Children.Add(selection);
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);

        var footer = new StackPanel();
        if (scan.Warnings.Count > 0)
            footer.Children.Add(new TextBlock { Text = "部分桌面项目无法读取，已列出其余可用项目。",
                ToolTip = string.Join(Environment.NewLine, scan.Warnings), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "取消", MinWidth = 76, IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        RegisterName("AddSelectedButton", _add);
        _add.Click += (_, _) => { if (SelectedPaths.Count > 0) DialogResult = true; };
        actions.Children.Add(cancel); actions.Children.Add(_add); footer.Children.Add(actions);
        DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);

        var list = _list;
        AutomationProperties.SetName(list, "桌面项目");
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        // 在列表内消费滚轮，避免复选框及父窗口把滚轮交给启动台翻页。
        list.PreviewMouseWheel += (_, args) =>
        {
            if (list.Template.FindName("PART_ScrollViewer", list)
                is not ScrollViewer scroll || scroll.ScrollableHeight <= 0) return;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset
                - args.Delta / 120d * 3);
            args.Handled = true;
        };
        RegisterName("DesktopItemsList", list);
        foreach (var item in scan.Items)
        {
            var icon = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0), Stretch = Stretch.Uniform };
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = item.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis });
            label.Children.Add(new TextBlock { Text = (item.FromCurrentUser ? "个人桌面" : "公共桌面")
                + (item.IsDirectory ? " · 文件夹" : ""), FontSize = 11, Opacity = .65, Margin = new Thickness(0, 3, 0, 0) });
            var row = new DockPanel();
            DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon); row.Children.Add(label);
            var choice = new CheckBox { Content = row, ToolTip = item.Path, Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(4, 5, 4, 5) };
            AutomationProperties.SetName(choice, item.DisplayName);
            choice.Checked += (_, _) => UpdateSelection(); choice.Unchecked += (_, _) => UpdateSelection();
            _rows.Add((item, choice, icon)); list.Items.Add(choice);
        }
        var listHost = new Grid();
        listHost.Children.Add(list);
        var empty = new TextBlock { Text = "桌面上没有可添加的文件或快捷方式。",
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Opacity = .75,
                Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed };
        listHost.Children.Add(empty); panel.Children.Add(listHost);
        search.TextChanged += (_, _) =>
        {
            var query = search.Text.Trim();
            hint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            // 只过滤显示，不重建复选框；隐藏条目的勾选状态继续保留。
            list.Items.Filter = value => value is CheckBox { Tag: DesktopItem item }
                && (item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
            empty.Text = _rows.Count == 0 ? "桌面上没有可添加的文件或快捷方式。"
                : "没有匹配的桌面项目。";
            empty.Visibility = list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelection();
        };
        var surface = new Grid();
        // 导入对话框使用静态深色底，避免文字叠在启动台上，也省去重复背景采集。
        surface.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 31, 35))
        });
        surface.Children.Add(panel); Content = surface;
        UpdateSelection();
        Loaded += (_, _) => { search.Focus(); LoadIcons(); };
        Closed += (_, _) => { _closed = true; _icons?.Dispose(); };
    }

    private void SelectAll(bool selected)
    {
        _changingSelection = true;
        try
        {
            // 全选仅作用于搜索结果；取消全选同时清除隐藏条目的选择。
            foreach (var row in _rows)
                if (!selected || _list.Items.Contains(row.Choice))
                    row.Choice.IsChecked = selected;
        }
        finally { _changingSelection = false; UpdateSelection(); }
    }

    private void UpdateSelection()
    {
        if (_changingSelection) return;
        var count = _rows.Count(row => row.Choice.IsChecked == true);
        _selectionCount.Text = _list.Items.Count == _rows.Count
            ? $"已选 {count} / {_rows.Count} 项"
            : $"已选 {count} 项 · 找到 {_list.Items.Count} 项";
        _add.IsEnabled = count > 0;
    }

    private void LoadIcons()
    {
        if (_icons is not null || _closed || _rows.Count == 0) return;
        _icons = new LauncherIconLoader();
        foreach (var row in _rows) _ = LoadIconAsync(row.Item.Path, row.Icon);
    }

    private async Task LoadIconAsync(string path, Image host)
    {
        var image = await _icons!.LoadAsync(path, () =>
        {
            var extracted = IconExtractor.Extract(path);
            if (extracted is null) return null;
            using var stream = new MemoryStream(PngWriter.EncodeBgra(extracted.Bgra, extracted.Width, extracted.Height));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = 48; bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }).ConfigureAwait(false);
        if (image is null || host.Dispatcher.HasShutdownStarted) return;
        try
        {
            await host.Dispatcher.InvokeAsync(() => { if (!_closed) host.Source = image; },
                System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (TaskCanceledException) { }
    }
}
