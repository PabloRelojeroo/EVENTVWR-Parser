using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EventvwrParser.Localization;
using EventvwrParser.Models;
using EventvwrParser.Services;

namespace EventvwrParser;

public partial class MainWindow : Window {
    static readonly (Localized group, Localized[] types)[] Layout = {
        (Strings.GroupPowerShell, new[] { Strings.TypeCommand, Strings.TypeScript, Strings.TypePipeline, Strings.TypePsReadLine, Strings.TypePowerShellProcess }),
        (Strings.GroupJournal, new Localized[0]),
        (Strings.GroupTasks, new[] { Strings.TypeTaskStart, Strings.TypeTaskEnd, Strings.TypeTaskChange }),
        (Strings.GroupLogs, new[] { Strings.TypeLogCleared }),
        (Strings.GroupSystem, new[] { Strings.TypeServiceInstalled, Strings.TypeProcessCreated }),
    };

    readonly DispatcherTimer _debounce;
    List<LogEvent> _events = new();
    List<Localized> _warnings = new();
    DateTime _boot;
    DateTime _logon;
    ICollectionView? _view;
    Func<LogEvent, bool> _category = _ => true;
    string _categoryKey = "all";
    HashSet<int>? _ids;
    string[] _terms = Array.Empty<string>();
    bool _rebuilding;

    public MainWindow() {
        InitializeComponent();
        ApplyLanguage();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Tick += (_, _) => {
            _debounce.Stop();
            ApplyInputs();
        };
        SourceInitialized += (_, _) => UseDarkTitleBar();
        Loaded += async (_, _) => await LoadAsync();
    }

    async Task LoadAsync() {
        RefreshButton.IsEnabled = false;
        LoadingOverlay.Visibility = Visibility.Visible;
        StatusText.Text = Strings.ReadingEvents;
        _warnings = new();
        ShowWarnings();

        _boot = SessionInfo.BootTime;
        _logon = SessionInfo.LogonTime;
        ShowSessionTimes();

        var sw = Stopwatch.StartNew();
        var result = await Task.Run(() => EventCollector.Collect(_logon));
        sw.Stop();

        _events = result.Events;
        _view = CollectionViewSource.GetDefaultView(_events);
        _view.Filter = Filter;
        EventsGrid.ItemsSource = _view;
        _warnings = result.Warnings;
        ShowWarnings();

        ParseInputs();
        RebuildCategories();
        UpdateStatus(Strings.LoadedIn(sw.Elapsed.TotalSeconds));

        LoadingOverlay.Visibility = Visibility.Collapsed;
        RefreshButton.IsEnabled = true;
    }

    void ApplyLanguage() {
        LanguageButton.Content = Strings.LanguageButton;
        LanguageButton.ToolTip = Strings.LanguageTip;
        LogonChip.ToolTip = Strings.LogonTip;
        SearchBox.Tag = Strings.SearchHint;
        RefreshButton.Content = Strings.Refresh;
        OnlyInstance.Content = Strings.OnlyInstance;
        HideNoise.Content = Strings.HideDuplicates;
        CategoriesCaption.Text = Strings.Categories;
        TimeColumn.Header = Strings.ColumnTime;
        TypeColumn.Header = Strings.ColumnType;
        SummaryColumn.Header = Strings.ColumnSummary;
        LoadingTitle.Text = Strings.ReadingEvents;
        LoadingHint.Text = Strings.LoadingHint;
        CopyButton.Content = Strings.Copy;
        ShowSelection();
    }

    void ShowSessionTimes() {
        BootText.Text = $"Boot  {Strings.ShortDate(_boot)}";
        LogonText.Text = $"Logon  {Strings.ShortDate(_logon)}";
    }

    void ShowWarnings() {
        WarnText.Text = string.Join(" | ", _warnings);
        WarnText.ToolTip = string.Join("\n", _warnings);
    }

    bool BaseFilter(LogEvent e) =>
        (OnlyInstance.IsChecked != true || e.InInstance)
        && (HideNoise.IsChecked != true || !e.IsNoise)
        && (_ids == null || _ids.Contains(e.Id));

    bool Filter(object o) => o is LogEvent e && BaseFilter(e) && _category(e) && MatchesSearch(e);

    bool MatchesSearch(LogEvent e) {
        foreach (var term in _terms) {
            if (!(Has(e.Type, term) || Has(e.Group, term) || Has(e.Summary, term) || Has(e.Details, term)
                  || Has(e.Log, term) || Has(e.IdText, term)
                  || (e.IsFileless && Has("fileless", term))))
                return false;
        }
        return true;
    }

    static bool Has(object s, string term) => s.ToString()!.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

    void ParseInputs() {
        _terms = SearchBox.Text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var ids = new HashSet<int>();
        foreach (var part in IdsBox.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
            var range = part.Split('-');
            if (range.Length == 2 && int.TryParse(range[0], out var a) && int.TryParse(range[1], out var b)) {
                for (var i = Math.Min(a, b); i <= Math.Max(a, b) && ids.Count < 10000; i++) ids.Add(i);
            } else if (int.TryParse(part, out var id)) {
                ids.Add(id);
            }
        }
        _ids = ids.Count > 0 ? ids : null;
    }

    void ApplyInputs() {
        if (_view == null) return;
        ParseInputs();
        RebuildCategories();
        _view.Refresh();
        UpdateStatus();
    }

    void RebuildCategories() {
        var text = (Brush)FindResource("Text");
        var muted = (Brush)FindResource("Muted");
        var visible = _events.Where(BaseFilter).ToList();

        var items = new List<CategoryItem> {
            new() { Key = "all", Label = Strings.AllEvents, Match = _ => true, Weight = FontWeights.SemiBold, Brush = text },
            new() { Key = "fileless", Label = "FileLess", Match = e => e.IsFileless, Weight = FontWeights.SemiBold, Brush = (Brush)FindResource("Red") },
        };

        foreach (var (group, types) in Layout) {
            var g = group;
            items.Add(new CategoryItem {
                Key = "g:" + g.Id,
                Label = g.ToString(),
                Match = e => e.Group == g,
                Weight = FontWeights.SemiBold,
                Brush = text,
                Indent = new Thickness(0, 10, 0, 0),
            });
            foreach (var type in types) {
                var t = type;
                items.Add(new CategoryItem {
                    Key = "t:" + g.Id + ":" + t.Id,
                    Label = t.ToString(),
                    Match = e => e.Group == g && e.Type == t,
                    Brush = muted,
                    Indent = new Thickness(12, 0, 0, 0),
                });
            }
        }

        foreach (var item in items) item.Count = visible.Count(item.Match);

        _rebuilding = true;
        CategoryList.ItemsSource = items;
        CategoryList.SelectedItem = items.FirstOrDefault(i => i.Key == _categoryKey) ?? items[0];
        _rebuilding = false;
    }

    void UpdateStatus(string? extra = null) {
        if (_view == null) return;
        var shown = _view.Cast<object>().Count();
        var fileless = _events.Count(e => e.IsFileless);
        StatusText.Text = Strings.Status(_events.Count, shown, fileless)
                          + (extra != null ? $" | {extra}" : "");
    }

    void OnInputChanged(object sender, TextChangedEventArgs e) {
        if (!IsLoaded) return;
        _debounce.Stop();
        _debounce.Start();
    }

    void OnToggle(object sender, RoutedEventArgs e) {
        if (!IsLoaded) return;
        ApplyInputs();
    }

    void OnCategoryChanged(object sender, SelectionChangedEventArgs e) {
        if (CategoryList.SelectedItem is not CategoryItem item) return;
        _category = item.Match;
        _categoryKey = item.Key;
        if (_rebuilding || _view == null) return;
        _view.Refresh();
        UpdateStatus();
    }

    void OnEventSelected(object sender, SelectionChangedEventArgs e) => ShowSelection();

    void ShowSelection() {
        if (EventsGrid.SelectedItem is not LogEvent ev) {
            DetTitle.Text = Strings.SelectEvent;
            DetMeta.Text = "";
            DetReasons.ItemsSource = null;
            DetText.Text = "";
            return;
        }

        DetTitle.Text = ev.TypeText;
        var meta = $"{ev.Log} | ID {ev.IdText} | {ev.TimeText} | {(ev.InInstance ? Strings.InInstance : Strings.OutOfInstance)}";
        if (ev.Related > 0) meta += $" | {Strings.Duplicates(ev.Related)}";
        if (ev.TimeApprox) meta += $" | {Strings.ApproxTime}";
        DetMeta.Text = meta;
        DetReasons.ItemsSource = ev.Reasons.Select(r => r.ToString()).ToList();
        DetText.Text = ev.Details.ToString();
        DetText.ScrollToHome();
    }

    async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    void OnLanguage(object sender, RoutedEventArgs e) {
        Strings.ToggleLanguage();
        ApplyLanguage();
        ShowSessionTimes();
        ShowWarnings();
        ApplyInputs();
        if (LoadingOverlay.Visibility == Visibility.Visible) StatusText.Text = Strings.ReadingEvents;
    }

    void OnCopy(object sender, RoutedEventArgs e) {
        if (EventsGrid.SelectedItem is not LogEvent ev) return;
        try {
            Clipboard.SetText($"[{ev.TimeText}] {ev.Log} ID {ev.IdText} - {ev.Type}\n{string.Join("\n", ev.Reasons.Select(r => "• " + r))}\n\n{ev.Details}");
        } catch {
        }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    void UseDarkTitleBar() {
        var hwnd = new WindowInteropHelper(this).Handle;
        var on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));
    }
}
