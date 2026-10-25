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
    private string? _file;
    private Transcript? _transcript;
    private CancellationTokenSource? _cts;

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
        };
        VersionText.Text = "Versión " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?") + " · Copyright (C) 2026 Dathinel";
        CueList.ItemsSource = _cues;
        _cues.CollectionChanged += (_, _) => EmptyState.Visibility = _cues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach ((string id, string name) in AudioLanguages)
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }
        LanguageBox.SelectedIndex = 0;
        TranslateBox.Items.Add(new ComboBoxItem { Content = "No traducir", Tag = "" });
        foreach ((string id, string name) in SubtitleTranslator.Languages)
        {
            TranslateBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }
        TranslateBox.SelectedIndex = 0;
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

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) ? DataPackageOperation.Copy : DataPackageOperation.None;

    private async void OnDrop(object sender, DragEventArgs e) => await GuardedAsync(async () =>
    {
        IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
        if (items.FirstOrDefault() is IStorageFile file)
        {
            await LoadFileAsync(file.Path);
        }
    });

    private nint Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private async void OnChoose(object sender, RoutedEventArgs e) => await GuardedAsync(async () =>
    {
        if (FileDialogs.OpenSingle(Hwnd, "Elegir video o audio", FileDialogs.MediaFilter, FileDialogs.AllFilter) is { } path)
        {
            await LoadFileAsync(path);
        }
    });

    private void OnChooseFolder(object sender, RoutedEventArgs e)
    {
        if (FileDialogs.ChooseFolder(Hwnd, "Carpeta de salida") is { } folder)
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

    private async Task LoadFileAsync(string path)
    {
        Notice.IsOpen = false;
        if (!MediaInfo.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
        {
            ShowError($"«{Path.GetFileName(path)}» no es un video ni un audio que Subtitular reconozca ({string.Join(", ", MediaInfo.Extensions)}).");
            return;
        }
        _file = path;
        _transcript = null;
        _cues.Clear();
        SaveButton.IsEnabled = false;
        FileName.Text = Path.GetFileName(path);
        FileInfo.Text = path;
        RunButton.IsEnabled = true;

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

    private JobOptions Options() => new(_file!)
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
        if (_file is null)
        {
            return;
        }
        _cues.Clear();
        _transcript = null;
        SaveButton.IsEnabled = false;
        Transcript? transcript = await BusyAsync((options, progress, token) => SubtitleJob.RunAsync(options, progress, token));
        if (transcript is null)
        {
            return;
        }
        _transcript = transcript;
        foreach (SubtitleCue cue in SubtitlePreview.Cues(transcript))
        {
            _cues.Add(new CueItem(cue));
        }
        SaveButton.IsEnabled = _cues.Count > 0;
        Show(InfoBarSeverity.Informational, $"{_cues.Count} bloques en «{transcript.Language}». Revisa el texto y pulsa Guardar.");
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_transcript is null)
        {
            return;
        }
        Transcript edited = SubtitlePreview.ToTranscript(_cues.Select(c => (c.Start, c.End, c.Text)), _transcript);
        (string Subtitles, string? Audio)? saved = await BusyAsync((options, progress, token) => SubtitleJob.SaveAsync(edited, options, progress, token));
        if (saved is { } s)
        {
            Show(InfoBarSeverity.Success, s.Audio is null ? $"Guardado: {s.Subtitles}" : $"Guardados: {s.Subtitles} y {s.Audio}");
        }
    }

    /// <summary>Corre una etapa con progreso y Cancelar; los errores previstos salen en la barra, en español.</summary>
    private async Task<T?> BusyAsync<T>(Func<JobOptions, IProgress<JobProgress>, CancellationToken, Task<T>> work)
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
        JobProgress? last = null;
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += (_, _) => StageText.Text = last?.Status(clock.Elapsed) ?? "Preparando · " + JobProgress.FormatClock(clock.Elapsed);
        timer.Start();
        Progress<JobProgress> progress = new(p =>
        {
            last = p;
            Progress.IsIndeterminate = false;
            StageText.Text = p.Status(clock.Elapsed);
            BarAnim.To = p.Overall * 100;
            BarStory.Begin();
        });
        try
        {
            T result = await work(Options(), progress, _cts.Token);
            DoneStory.Begin();
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
            RunButton.IsEnabled = true;
            SaveButton.IsEnabled = _transcript is not null;
            CancelButton.IsEnabled = false;
        }
    }
}
