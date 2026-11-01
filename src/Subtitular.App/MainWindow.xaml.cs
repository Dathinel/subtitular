using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Subtitular.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT;

namespace Subtitular.App;

/// <summary>Un bloque de la vista previa: tiempo fijo, texto editable (x:Bind TwoWay).</summary>
internal sealed partial class CueItem(SubtitleCue cue)
{
    public TimeSpan Start { get; } = cue.Start;

    public TimeSpan End { get; } = cue.End;

    public string Text { get; set; } = SubtitlePreview.Text(cue);

    public string Time => $"{SubtitleWriter.FormatSrtTimestamp(Start)} → {SubtitleWriter.FormatSrtTimestamp(End)}";
}

public sealed partial class MainWindow : Window
{
    private static readonly (string Id, string Name)[] AudioLanguages =
    [
        ("auto", "Detectar"), ("es", "Español"), ("en", "Inglés"), ("pt", "Portugués"), ("fr", "Francés"), ("it", "Italiano"),
        ("de", "Alemán"), ("ca", "Catalán"), ("gl", "Gallego"), ("eu", "Euskera"), ("ja", "Japonés"), ("zh", "Chino"),
    ];

    private readonly ObservableCollection<CueItem> _cues = [];
    /// <summary>La cola: uno o varios archivos con los mismos ajustes.</summary>
    private readonly List<string> _files = [];
    /// <summary>El archivo al que pertenece la vista previa (con varios, el último que salió bien).</summary>
    private string? _previewFile;
    private Transcript? _transcript;
    private CancellationTokenSource? _cts;
    private Preferences _prefs;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "subtitular.ico"));
        // Con AOT, el cast directo del presentador falla (InvalidCastException): hay que pasar por As<>.
        OverlappedPresenter presenter = AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.PreferredMinimumWidth = WindowPlacement.MinWidth;
        presenter.PreferredMinimumHeight = WindowPlacement.MinHeight;
        // Vuelve donde quedó, salvo que esa pantalla ya no exista.
        if (WindowPlacement.Load(WindowPlacement.DefaultPath) is { } placement
            && DisplayArea.GetFromPoint(new PointInt32(placement.X + 32, placement.Y + 32), DisplayAreaFallback.None) is not null)
        {
            AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
        }
        else
        {
            AppWindow.Resize(new SizeInt32(1000, 800));
        }
        Closed += (_, _) =>
        {
            if (presenter.State == OverlappedPresenterState.Restored)
            {
                new WindowPlacement(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height).Save(WindowPlacement.DefaultPath);
            }
            SavePreferences();
        };
        VersionText.Text = "Versión " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?") + " · Copyright (C) 2026 Dathinel";
        CueList.ItemsSource = _cues;
        _cues.CollectionChanged += (_, _) => EmptyState.Visibility = _cues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach ((string id, string name) in AudioLanguages)
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }
        TranslateBox.Items.Add(new ComboBoxItem { Content = "No traducir", Tag = "" });
        foreach ((string id, string name) in SubtitleTranslator.Languages)
        {
            TranslateBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }
        // Lo último elegido: idioma, formato, traducción, voz, carpeta de salida y si se abre al terminar.
        _prefs = Preferences.Load(Preferences.DefaultPath);
        SelectTag(LanguageBox, _prefs.Language);
        SelectTag(FormatBox, _prefs.Format);
        SelectTag(TranslateBox, _prefs.TranslateTo ?? "");
        SpeakBox.IsChecked = _prefs.Speak;
        OpenFolderBox.IsChecked = _prefs.OpenFolder;
        OutputDirBox.Text = _prefs.OutputDir ?? "";
        // Se guardan al cambiar, no solo al cerrar: un apagón o un cierre forzado no las pierde.
        LanguageBox.SelectionChanged += (_, _) => SavePreferences();
        FormatBox.SelectionChanged += (_, _) => SavePreferences();
        TranslateBox.SelectionChanged += (_, _) => SavePreferences();
        OutputDirBox.TextChanged += (_, _) => SavePreferences();
        foreach (CheckBox box in new[] { SpeakBox, OpenFolderBox })
        {
            box.Checked += (_, _) => SavePreferences();
            box.Unchecked += (_, _) => SavePreferences();
        }
        CompositionTarget.Rendering += OnFirstFrame;
    }

    private void OnFirstFrame(object? sender, object e)
    {
        CompositionTarget.Rendering -= OnFirstFrame;
        Program.FirstFrame();
    }

    public void ShowError(string message) => Show(InfoBarSeverity.Error, message);

    private void Show(InfoBarSeverity severity, string message)
    {
        Notice.Severity = severity;
        Notice.Message = message;
        Notice.IsOpen = true;
    }

    private static string Tag(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    /// <summary>Elige el elemento con esa etiqueta; si no está (idioma retirado), el primero.</summary>
    private static void SelectTag(ComboBox box, string tag)
    {
        int index = box.Items.ToList().FindIndex(i => (i as ComboBoxItem)?.Tag as string == tag);
        box.SelectedIndex = index >= 0 ? index : 0;
    }

    private void SavePreferences() => CurrentPreferences().Save(Preferences.DefaultPath);

    private Preferences CurrentPreferences() => _prefs with
    {
        OutputDir = string.IsNullOrWhiteSpace(OutputDirBox.Text) ? null : OutputDirBox.Text,
        Language = Tag(LanguageBox),
        Format = Tag(FormatBox),
        TranslateTo = Tag(TranslateBox) is { Length: > 0 } t ? t : null,
        Speak = SpeakBox.IsChecked == true,
        OpenFolder = OpenFolderBox.IsChecked == true,
    };

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) ? DataPackageOperation.Copy : DataPackageOperation.None;

    private async void OnDrop(object sender, DragEventArgs e) => await GuardedAsync(async () =>
    {
        IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
        await LoadFilesAsync([.. items.OfType<IStorageFile>().Select(f => f.Path)]);
    });

    private nint Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private async void OnChoose(object sender, RoutedEventArgs e) => await GuardedAsync(async () =>
    {
        IReadOnlyList<string> paths = FileDialogs.OpenMany(Hwnd, "Elegir video o audio", _prefs.LastInputDir, true, FileDialogs.MediaFilter, FileDialogs.AllFilter);
        if (paths.Count > 0)
        {
            await LoadFilesAsync(paths);
        }
    });

    private void OnChooseFolder(object sender, RoutedEventArgs e)
    {
        string? current = string.IsNullOrWhiteSpace(OutputDirBox.Text) ? _prefs.LastInputDir : OutputDirBox.Text;
        if (FileDialogs.ChooseFolder(Hwnd, "Carpeta de salida", current) is { } folder)
        {
            OutputDirBox.Text = folder;
        }
    }

    /// <summary>Lo que escape de un async void tras un await no llega a Application.UnhandledException y cierra la app: aquí sale en la barra.</summary>
    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(SubtitleJob.Describe(ex) ?? "Error inesperado: " + ex.Message);
        }
    }

    /// <summary>Carga la cola: uno o varios archivos. Los que no son video ni audio se avisan y se dejan fuera.</summary>
    private async Task LoadFilesAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }
        if (_cts is not null)
        {
            ShowError("Hay un trabajo en marcha: espera a que termine o pulsa Cancelar antes de cambiar los archivos.");
            return;
        }
        Notice.IsOpen = false;
        List<string> valid = [.. paths.Where(p => MediaInfo.Extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))];
        List<string> rejected = [.. paths.Except(valid).Select(p => Path.GetFileName(p))];
        if (rejected.Count > 0)
        {
            string names = string.Join(", ", rejected.Select(n => $"«{n}»"));
            ShowError($"{names} {(rejected.Count == 1 ? "no es" : "no son")} un video ni un audio que Subtitular reconozca ({string.Join(", ", MediaInfo.Extensions)}).");
        }
        if (valid.Count == 0)
        {
            return;
        }
        _files.Clear();
        _files.AddRange(valid);
        _prefs = _prefs with { LastInputDir = Path.GetDirectoryName(Path.GetFullPath(valid[0])) };
        SavePreferences();
        _previewFile = null;
        _transcript = null;
        _cues.Clear();
        SaveButton.IsEnabled = false;
        RunButton.IsEnabled = true;

        if (valid.Count > 1)
        {
            FileName.Text = $"{valid.Count} archivos en cola";
            IEnumerable<string> shown = valid.Take(5).Select(p => Path.GetFileName(p));
            FileInfo.Text = string.Join(", ", shown) + (valid.Count > 5 ? $" y {valid.Count - 5} más" : "") + " · cada uno se guarda al terminar";
            return;
        }

        string path = valid[0];
        FileName.Text = Path.GetFileName(path);
        FileInfo.Text = path;
        List<string> info = [];
        try
        {
            if (await MediaInfo.DurationAsync(path) is { } duration)
            {
                info.Add("Duración " + duration.ToString(@"h\:mm\:ss"));
            }
        }
        catch (Exception ex)
        {
            info.Add(SubtitleJob.Describe(ex) ?? "sin duración: " + ex.Message);
        }
        IReadOnlyList<string> existing = MediaInfo.ExistingSubtitles(path);
        info.Add(existing.Count == 0 ? "sin subtítulos al lado" : "ya tiene subtítulos: " + string.Join(", ", existing.Select(Path.GetFileName)));
        FileInfo.Text = string.Join(" · ", info);
    }

    private JobOptions Options(string file) => new(file)
    {
        Language = Tag(LanguageBox),
        Format = Tag(FormatBox),
        OutputDir = string.IsNullOrWhiteSpace(OutputDirBox.Text) ? null : OutputDirBox.Text,
        TranslateTo = Tag(TranslateBox) is { Length: > 0 } t ? t : null,
        Speak = SpeakBox.IsChecked == true,
    };

    private void OnCancel(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void OnAbout(object sender, RoutedEventArgs e) => FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0 || _cts is not null)
        {
            return;
        }
        _cues.Clear();
        _transcript = null;
        _previewFile = null;
        SaveButton.IsEnabled = false;
        if (_files.Count == 1)
        {
            string file = _files[0];
            Transcript? transcript = await BusyAsync((progress, token) => SubtitleJob.RunAsync(Options(file), Single(progress, file), token));
            if (transcript is null)
            {
                return;
            }
            ShowPreview(file, transcript);
            Show(InfoBarSeverity.Informational, $"{_cues.Count} bloques en «{transcript.Language}». Revisa el texto y pulsa Guardar.");
            return;
        }

        // Varios: cada uno se transcribe y se guarda seguido; la vista previa queda con el último que salió bien.
        List<JobOptions> jobs = [.. _files.Select(Options)];
        IReadOnlyList<BatchResult>? results = await BusyAsync((progress, token) => JobBatch.RunAsync(jobs, progress, token));
        if (results is null)
        {
            return;
        }
        if (results.LastOrDefault(r => r.Ok) is { Transcript: { } last } ok)
        {
            ShowPreview(ok.Options.InputFile, last);
        }
        // Cancelar no tira lo ya guardado: el resumen dice cuántos salieron y en cuál se paró.
        Show(results.All(r => r.Ok) ? InfoBarSeverity.Success : InfoBarSeverity.Warning, JobBatch.Summary(results));
        OpenResultFolder([.. results.Where(r => r.Ok).SelectMany(r => new[] { r.Subtitles, r.Audio })]);
    }

    private void ShowPreview(string file, Transcript transcript)
    {
        _previewFile = file;
        _transcript = transcript;
        foreach (SubtitleCue cue in SubtitlePreview.Cues(transcript))
        {
            _cues.Add(new CueItem(cue));
        }
        SaveButton.IsEnabled = _cues.Count > 0;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_transcript is null || _previewFile is null || _cts is not null)
        {
            return;
        }
        string file = _previewFile;
        Transcript edited = SubtitlePreview.ToTranscript(_cues.Select(c => (c.Start, c.End, c.Text)), _transcript);
        (string Subtitles, string? Audio)? saved = await BusyAsync((progress, token) => SubtitleJob.SaveAsync(edited, Options(file), Single(progress, file), token));
        if (saved is { } s)
        {
            Show(InfoBarSeverity.Success, s.Audio is null ? $"Guardado: {s.Subtitles}" : $"Guardados: {s.Subtitles} y {s.Audio}");
            OpenResultFolder([s.Subtitles, s.Audio]);
        }
    }

    /// <summary>Abre el Explorador en el resultado si la casilla está marcada; si no se puede, no pasa nada.</summary>
    private void OpenResultFolder(IReadOnlyList<string?> outputs)
    {
        if (OpenFolderBox.IsChecked != true || ResultFolder.ExplorerCommand(outputs) is not { } command)
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(command.Exe, command.Arguments) { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    /// <summary>El progreso de un solo archivo, en la misma barra que la cola.</summary>
    private static IProgress<JobProgress> Single(IProgress<BatchProgress> progress, string file) =>
        new SyncProgress<JobProgress>(p => progress.Report(new BatchProgress(0, 1, file, p)));

    /// <summary>Corre una etapa con progreso y Cancelar; los errores previstos salen en la barra, en español.</summary>
    private async Task<T?> BusyAsync<T>(Func<IProgress<BatchProgress>, CancellationToken, Task<T>> work)
    {
        Notice.IsOpen = false;
        _cts = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        DoneIcon.Opacity = 0;
        DoneScale.ScaleX = DoneScale.ScaleY = 0.6;
        // Hasta el primer aviso del módulo (cargar el modelo) la barra late indeterminada y el reloj corre.
        Progress.IsIndeterminate = true;
        Stopwatch clock = Stopwatch.StartNew();
        BatchProgress? last = null;
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += (_, _) => StageText.Text = last?.Status(clock.Elapsed) ?? "Preparando · " + JobProgress.FormatClock(clock.Elapsed);
        timer.Start();
        Progress<BatchProgress> progress = new(p =>
        {
            last = p;
            StageText.Text = p.Status(clock.Elapsed);
            if (p.Progress is null)
            {
                // Empieza otro archivo de la cola: vuelve a latir hasta que su módulo avise.
                Progress.IsIndeterminate = true;
                return;
            }
            Progress.IsIndeterminate = false;
            BarAnim.To = p.Overall * 100;
            BarStory.Begin();
        });
        try
        {
            T result = await work(progress, _cts.Token);
            if (!_cts.IsCancellationRequested)
            {
                DoneStory.Begin();
            }
            return result;
        }
        catch (Exception ex)
        {
            ShowError(SubtitleJob.Describe(ex) ?? "Error inesperado: " + ex.Message);
            return default;
        }
        finally
        {
            timer.Stop();
            _cts.Dispose();
            _cts = null;
            StageText.Text = "";
            BarStory.Stop();
            Progress.IsIndeterminate = false;
            Progress.Value = 0;
            RunButton.IsEnabled = _files.Count > 0;
            SaveButton.IsEnabled = _transcript is not null;
            CancelButton.IsEnabled = false;
        }
    }
}
