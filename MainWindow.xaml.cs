using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EventvwrParser.Models;
using EventvwrParser.Services;

namespace EventvwrParser;

public partial class MainWindow : Window {
    static readonly (string group, string[] types)[] Layout = {
        (EventCollector.GroupPowerShell, new[] { "Comando ejecutado", "Script ejecutado", "Pipeline .NET / cmdlet", "Historial PSReadLine", "Proceso PowerShell" }),
        (EventCollector.GroupJournal, new string[0]),
        (EventCollector.GroupTasks, new[] { EventCollector.TaskStart, EventCollector.TaskEnd, EventCollector.TaskChange }),
        (EventCollector.GroupLogs, new[] { "Log de eventos borrado" }),
        (EventCollector.GroupSystem, new[] { "Servicio instalado", "Proceso creado" }),
    };

    readonly DispatcherTimer _debounce;
    List<LogEvent> _events = new();
    ICollectionView? _view;
    Func<LogEvent, bool> _category = _ => true;
    string _categoryKey = "all";
    HashSet<int>? _ids;
    string[] _terms = Array.Empty<string>();
    bool _rebuilding;

    public MainWindow() {
        InitializeComponent();
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
        StatusText.Text = "Leyendo eventos...";
        WarnText.Text = "";

        var boot = SessionInfo.BootTime;
        var logon = SessionInfo.LogonTime;
        BootText.Text = $"Boot  {boot:dd/MM HH:mm}";
        LogonText.Text = $"Logon  {logon:dd/MM HH:mm}";

        var sw = Stopwatch.StartNew();
        var result = await Task.Run(() => EventCollector.Collect(logon));
        sw.Stop();

        _events = result.Events;
        _view = CollectionViewSource.GetDefaultView(_events);
        _view.Filter = Filter;
        EventsGrid.ItemsSource = _view;
        WarnText.Text = string.Join(" | ", result.Warnings);
        WarnText.ToolTip = string.Join("\n", result.Warnings);

        ParseInputs();
        RebuildCategories();
        UpdateStatus($"cargado en {sw.Elapsed.TotalSeconds:0.0}s");

        LoadingOverlay.Visibility = Visibility.Collapsed;
        RefreshButton.IsEnabled = true;
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
                  || (e.IsFileless && Has("fileless", term)) || (e.IsAntiForensic && Has("anti-forense", term))))
                return false;
        }
        return true;
    }

    static bool Has(string s, string term) => s.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

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
            new() { Key = "all", Label = "Todos", Match = _ => true, Weight = FontWeights.SemiBold, Brush = text },
            new() { Key = "fileless", Label = "FileLess", Match = e => e.IsFileless, Weight = FontWeights.SemiBold, Brush = (Brush)FindResource("Red") },
        };

        foreach (var (group, types) in Layout) {
            var g = group;
            items.Add(new CategoryItem {
                Key = "g:" + g,
                Label = g,
                Match = e => e.Group == g,
                Weight = FontWeights.SemiBold,
                Brush = text,
                Indent = new Thickness(0, 10, 0, 0),
            });
            foreach (var type in types) {
                var t = type;
                items.Add(new CategoryItem {
                    Key = "t:" + g + ":" + t,
                    Label = t,
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
        StatusText.Text = $"{_events.Count} eventos | mostrando {shown} | {fileless} fileless"
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

    void OnEventSelected(object sender, SelectionChangedEventArgs e) {
        if (EventsGrid.SelectedItem is not LogEvent ev) {
            DetTitle.Text = "Selecciona un evento";
            DetMeta.Text = "";
            DetReasons.ItemsSource = null;
            DetText.Text = "";
            return;
        }

        DetTitle.Text = ev.Type;
        var meta = $"{ev.Log} | ID {ev.IdText} | {ev.TimeText} | {(ev.InInstance ? "en instancia" : "fuera de instancia")}";
        if (ev.Related > 0) meta += $" | +{ev.Related} duplicados";
        if (ev.TimeApprox) meta += " | la hora es aprox (ultima vez q se modifico el archivo)";
        DetMeta.Text = meta;
        DetReasons.ItemsSource = ev.Reasons;
        DetText.Text = ev.Details;
        DetText.ScrollToHome();
    }

    async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

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
