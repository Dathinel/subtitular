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
    /// <summary>La transcripción de la que ya hay un WAV de voz (la cola o un Guardar anterior lo dejaron): Guardar no lo regenera si el texto no cambió.</summary>
    private Transcript? _spoken;
    private CancellationTokenSource? _cts;
    private Preferences _prefs;
    /// <summary>La versión nueva que muestra la franja, y el Lanzador si está en el PC.</summary>
    private ReleaseInfo? _release;
    private string? _launcher;

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
        SkipReviewBox.IsChecked = _prefs.SaveWithoutReview;
        UpdatesBox.IsChecked = _prefs.CheckUpdates;
        OutputDirBox.Text = _prefs.OutputDir ?? "";
        // Se guardan al cambiar, no solo al cerrar: un apagón o un cierre forzado no las pierde.
        LanguageBox.SelectionChanged += (_, _) => SavePreferences();
        FormatBox.SelectionChanged += (_, _) => SavePreferences();
        TranslateBox.SelectionChanged += (_, _) => SavePreferences();
        OutputDirBox.TextChanged += (_, _) => SavePreferences();
        foreach (CheckBox box in new[] { SpeakBox, OpenFolderBox, SkipReviewBox, UpdatesBox })
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
        // Con la ventana ya pintada: la consulta a GitHub (si toca) no retrasa el arranque.
        _ = GuardedAsync(() => CheckUpdatesAsync(manual: false));
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e) => await GuardedAsync(() => CheckUpdatesAsync(manual: true));

    /// <summary>
    /// Al arrancar solo si el aviso está activado y pasaron 24 h desde la última consulta; con «Buscar actualizaciones», siempre.
    /// La red va en el grupo de hilos; sin red o con respuesta rara, al arrancar no se dice nada.
    /// </summary>
    private async Task CheckUpdatesAsync(bool manual)
    {
        Preferences prefs = CurrentPreferences();
        CheckUpdatesButton.IsEnabled = false;
        UpdateCheckResult result;
        try
        {
            result = await Task.Run(() => UpdateCheck.CheckAsync(prefs, UpdateCheck.AppVersion, new GitHubReleaseFeed(), DateTimeOffset.UtcNow, manual, CancellationToken.None));
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
        if (result.Outcome == UpdateOutcome.NotDue)
        {
            return;
        }
        _prefs = result.Preferences;
        _prefs.Save(Preferences.DefaultPath);
        switch (result.Outcome)
        {
            case UpdateOutcome.NewVersion:
                ShowUpdate(result.Release!);
                break;
            case UpdateOutcome.UpToDate when manual:
                Show(InfoBarSeverity.Informational, $"No hay nada nuevo: Subtitular {UpdateCheck.AppVersion.ToString(3)} es la última versión.");
                break;
            case UpdateOutcome.Failed when manual:
                Show(InfoBarSeverity.Warning, "No se pudo consultar si hay una versión nueva. Comprueba la conexión y vuelve a intentarlo.");
                break;
        }
    }

    private void ShowUpdate(ReleaseInfo release)
    {
        _release = release;
        _launcher = Launcher.Find();
        UpdateText.Text = $"Subtitular {release.Version.ToString(3)} disponible";
        OpenLauncherButton.Visibility = _launcher is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateStrip.Visibility = Visibility.Visible;
    }

    private void OnViewRelease(object sender, RoutedEventArgs e)
    {
        if (_release is not null)
        {
            OpenExternal(_release.Url);
        }
    }

    private void OnOpenLauncher(object sender, RoutedEventArgs e)
    {
        if (_launcher is not null)
        {
            OpenExternal(_launcher);
        }
    }

    /// <summary>«Más tarde»: la franja se va y no se vuelve a avisar de esa versión (sí de la siguiente, y sí con «Buscar actualizaciones»).</summary>
    private void OnUpdateLater(object sender, RoutedEventArgs e)
    {
        if (_release is not null)
        {
            _prefs = UpdateCheck.Dismiss(CurrentPreferences(), _release);
            _prefs.Save(Preferences.DefaultPath);
        }
        UpdateStrip.Visibility = Visibility.Collapsed;
    }

    /// <summary>Abre una página en el navegador o un programa por el shell; si no se puede, lo dice la barra.</summary>
    private void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.FileNotFoundException)
        {
            ShowError($"No se pudo abrir «{target}»: {ex.Message}");
        }
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
        SaveWithoutReview = SkipReviewBox.IsChecked == true,
        CheckUpdates = UpdatesBox.IsChecked == true,
    };

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) ? DataPackageOperation.Copy : DataPackageOperation.None;

    /// <summary>Archivos y carpetas (de una carpeta entran sus videos y audios). Lo que no está en el disco (un adjunto, un elemento virtual) se avisa.</summary>
    private async void OnDrop(object sender, DragEventArgs e) => await GuardedAsync(async () =>
    {
        IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
        List<string> paths = [.. items.Where(i => i is IStorageFile or IStorageFolder).Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p))];
        if (paths.Count == 0)
        {
            ShowError("Lo que has soltado no es un archivo ni una carpeta del disco. Suelta videos o audios, o una carpeta con ellos.");
            return;
        }
        await LoadFilesAsync(paths);
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

    /// <summary>Carga la cola: archivos o carpetas (<see cref="MediaInfo.Select"/>). Lo que no es video ni audio se avisa y se deja fuera.</summary>
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
        MediaSelection selection = paths.Any(Directory.Exists)
            ? await Task.Run(() => MediaInfo.Select(paths))
            : MediaInfo.Select(paths);
        if (selection.Warning is { } warning)
        {
            ShowError(warning);
        }
        List<string> valid = [.. selection.Media];
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
        _spoken = null;
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
        _spoken = null;
        SaveButton.IsEnabled = false;
        if (SubtitleJob.ReviewsBeforeSaving(_files.Count, SkipReviewBox.IsChecked == true))
        {
            string file = _files[0];
            Transcript? transcript = await BusyAsync((progress, token) => SubtitleJob.RunAsync(Options(file), Single(progress, file), token));
            if (transcript is null)
            {
                return;
            }
            ShowPreview(file, transcript, null);
            Show(InfoBarSeverity.Informational, $"{_cues.Count} bloques en «{transcript.Language}». Revisa el texto y pulsa Guardar.");
            return;
        }

        // Varios (o uno con «Guardar sin revisar»): cada uno se transcribe y se guarda seguido; la vista previa queda con el último que salió bien.
        List<JobOptions> jobs = [.. _files.Select(Options)];
        IReadOnlyList<BatchResult>? results = await BusyAsync((progress, token) => JobBatch.RunAsync(jobs, progress, token));
        if (results is null)
        {
            return;
        }
        if (results.LastOrDefault(r => r.Ok) is { Transcript: { } last } ok)
        {
            ShowPreview(ok.Options.InputFile, last, ok.Audio is null ? null : last);
        }
        // Cancelar no tira lo ya guardado: el resumen dice cuántos salieron y en cuál se paró.
        if (results is [{ Ok: true, Subtitles: { } subtitles } only])
        {
            Show(InfoBarSeverity.Success, only.Audio is null ? $"Guardado: {subtitles}" : $"Guardados: {subtitles} y {only.Audio}");
        }
        else
        {
            Show(results.All(r => r.Ok) ? InfoBarSeverity.Success : InfoBarSeverity.Warning, JobBatch.Summary(results));
        }
        OpenResultFolder([.. results.Where(r => r.Ok).SelectMany(r => new[] { r.Subtitles, r.Audio })]);
    }

    /// <summary><paramref name="spoken"/>: la transcripción de la que ya quedó el WAV de voz, si lo hay.</summary>
    private void ShowPreview(string file, Transcript transcript, Transcript? spoken)
    {
        _previewFile = file;
        _transcript = transcript;
        _spoken = spoken;
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
        Transcript? spoken = _spoken;
        (string Subtitles, string? Audio)? saved = await BusyAsync((progress, token) => SubtitleJob.SaveAsync(edited, Options(file), Single(progress, file), token, spoken));
        if (saved is { } s)
        {
            // Si quedó un WAV, es el de este texto: otro Guardar sin cambios no lo vuelve a generar.
            _spoken = s.Audio is null ? null : edited;
            Show(InfoBarSeverity.Success, s.Audio is null ? $"Guardado: {s.Subtitles}" : $"Guardados: {s.Subtitles} y {s.Audio}");
            OpenResultFolder([s.Subtitles, s.Audio]);
        }
    }

    /// <summary>Abre el Explorador en el resultado si la casilla está marcada.</summary>
    private void OpenResultFolder(IReadOnlyList<string?> outputs)
    {
        if (OpenFolderBox.IsChecked == true)
        {
            ResultFolder.Open(outputs);
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
