using Subtitular.Core;

namespace Subtitular.Tests;

public sealed class MediaSelectionTests
{
    [Fact]
    public void A_folder_enqueues_its_media_by_name_without_subfolders()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-carpeta-").FullName;
        try
        {
            string folder = Path.Combine(dir, "temporada");
            Directory.CreateDirectory(Path.Combine(folder, "extras"));
            foreach (string name in new[] { "b.mkv", "A.mp4", "notas.txt", "c.MP3" })
            {
                File.WriteAllText(Path.Combine(folder, name), "");
            }
            File.WriteAllText(Path.Combine(folder, "extras", "d.mp4"), "");
            string loose = Path.Combine(dir, "suelto.wav");
            File.WriteAllText(loose, "");

            MediaSelection one = MediaInfo.Select([folder]);
            Assert.Equal(["A.mp4", "b.mkv", "c.MP3"], one.Media.Select(Path.GetFileName));
            Assert.Empty(one.Rejected);
            Assert.Empty(one.EmptyFolders);
            Assert.Null(one.Warning);

            // Archivos y carpetas mezclados, en el orden en que se soltaron; lo que no es un medio se avisa y se deja fuera.
            MediaSelection mixed = MediaInfo.Select([loose, Path.Combine(dir, "leeme.txt"), folder]);
            Assert.Equal(["suelto.wav", "A.mp4", "b.mkv", "c.MP3"], mixed.Media.Select(Path.GetFileName));
            Assert.Equal([Path.Combine(dir, "leeme.txt")], mixed.Rejected);
            Assert.StartsWith("«leeme.txt» no es un video ni un audio", mixed.Warning, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Folders_without_media_are_reported_and_nothing_is_enqueued()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-vacia-").FullName;
        try
        {
            string empty = Path.Combine(dir, "vacía");
            Directory.CreateDirectory(empty);
            string docs = Path.Combine(dir, "docs");
            Directory.CreateDirectory(docs);
            File.WriteAllText(Path.Combine(docs, "guion.docx"), "");

            MediaSelection selection = MediaInfo.Select([empty, docs + Path.DirectorySeparatorChar, Path.Combine(dir, "foto.png")]);
            Assert.Empty(selection.Media);
            Assert.Equal([empty, docs + Path.DirectorySeparatorChar], selection.EmptyFolders);
            Assert.Equal("«foto.png» no es un video ni un audio que Subtitular reconozca (" + string.Join(", ", MediaInfo.Extensions) + ")."
                + " Las carpetas «vacía», «docs» no tienen videos ni audios.", selection.Warning);
            Assert.Equal("La carpeta «vacía» no tiene videos ni audios.", MediaInfo.Select([empty]).Warning);
            Assert.Equal("«a.txt», «b.doc» no son un video ni un audio que Subtitular reconozca (" + string.Join(", ", MediaInfo.Extensions) + ").",
                MediaInfo.Select(["a.txt", "b.doc"]).Warning);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData(".mp4", true)]
    [InlineData(".MP4", true)]
    [InlineData(".mkv", true)]
    [InlineData(".MKV", true)]
    [InlineData(".mov", true)]
    [InlineData(".MOV", true)]
    [InlineData(".m4v", true)]
    [InlineData(".webm", true)]
    [InlineData(".avi", true)]
    [InlineData(".mts", true)]
    [InlineData(".m2ts", true)]
    [InlineData(".ts", true)]
    [InlineData(".wmv", true)]
    [InlineData(".flv", true)]
    [InlineData(".3gp", true)]
    [InlineData(".mp3", true)]
    [InlineData(".MP3", true)]
    [InlineData(".wav", true)]
    [InlineData(".WAV", true)]
    [InlineData(".m4a", true)]
    [InlineData(".aac", true)]
    [InlineData(".flac", true)]
    [InlineData(".FLAC", true)]
    [InlineData(".ogg", true)]
    [InlineData(".opus", true)]
    [InlineData(".txt", false)]
    [InlineData(".jpg", false)]
    [InlineData(".exe", false)]
    [InlineData(".srt", false)]
    [InlineData(".vtt", false)]
    [InlineData("", false)]
    public void IsMedia_recognizes_all_supported_extensions_case_insensitively(string extension, bool expected)
    {
        string fakePath = "sample" + extension;
        Assert.Equal(expected, MediaInfo.IsMedia(fakePath));
    }

    [Fact]
    public void Select_handles_non_existent_files_and_directories()
    {
        string nonExistentMedia = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid().ToString("N") + ".mp4");
        string nonExistentDoc = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid().ToString("N") + ".pdf");
        string nonExistentPath = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid().ToString("N"));

        MediaSelection selection = MediaInfo.Select([nonExistentMedia, nonExistentDoc, nonExistentPath]);

        Assert.Equal([nonExistentMedia], selection.Media);
        Assert.Equal([nonExistentDoc, nonExistentPath], selection.Rejected);
        Assert.Empty(selection.EmptyFolders);
    }

    [Fact]
    public void Select_handles_double_extensions_and_no_extension()
    {
        string doubleExtRejected = "video.mp4.bak";
        string tarGzRejected = "pelicula.tar.gz";
        string backupMedia = "clip.backup.mkv";
        string noExtRejected = "archivo_sin_extension";

        MediaSelection selection = MediaInfo.Select([doubleExtRejected, tarGzRejected, backupMedia, noExtRejected]);

        Assert.Equal([backupMedia], selection.Media);
        Assert.Equal([doubleExtRejected, tarGzRejected, noExtRejected], selection.Rejected);
    }

    [Fact]
    public void Select_handles_folders_with_hundreds_of_files_with_stable_sorting()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-cientos-").FullName;
        try
        {
            List<string> expectedNames = [];
            for (int i = 0; i < 120; i++)
            {
                string name = $"clip_{i:D3}.mp4";
                expectedNames.Add(name);
                File.WriteAllText(Path.Combine(dir, name), "");
            }
            for (int i = 0; i < 50; i++)
            {
                File.WriteAllText(Path.Combine(dir, $"nota_{i:D3}.txt"), "");
            }

            MediaSelection selection = MediaInfo.Select([dir]);

            Assert.Equal(120, selection.Media.Count);
            Assert.Equal(expectedNames, selection.Media.Select(p => Path.GetFileName(p)!).ToList());
            Assert.Empty(selection.Rejected);
            Assert.Empty(selection.EmptyFolders);
            Assert.Null(selection.Warning);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Select_handles_paths_with_spaces_accents_enye_and_symbols()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-tildes-").FullName;
        try
        {
            string subfolder = Path.Combine(dir, "carpeta con espacios y tildes - año 2026");
            Directory.CreateDirectory(subfolder);
            string file1 = Path.Combine(subfolder, "vídeo navideño con niño & familia.MP4");
            string file2 = Path.Combine(subfolder, "canción de cuna (año 2026) #1.WAV");
            File.WriteAllText(file1, "");
            File.WriteAllText(file2, "");

            MediaSelection selection = MediaInfo.Select([subfolder]);

            Assert.Equal(2, selection.Media.Count);
            // Orden alfabético: "canción..." viene antes de "vídeo..."
            Assert.Equal(Path.GetFileName(file2), Path.GetFileName(selection.Media[0]));
            Assert.Equal(Path.GetFileName(file1), Path.GetFileName(selection.Media[1]));
            Assert.Null(selection.Warning);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Select_preserves_input_order_for_loose_files_while_sorting_folder_contents()
    {
        string dir = Directory.CreateTempSubdirectory("subtitular-orden-").FullName;
        try
        {
            string f1 = Path.Combine(dir, "z_video.mp4");
            string f2 = Path.Combine(dir, "a_video.mp4");
            string f3 = Path.Combine(dir, "m_video.mp4");
            File.WriteAllText(f1, "");
            File.WriteAllText(f2, "");
            File.WriteAllText(f3, "");

            // Sueltos: preservan orden z, a, m
            MediaSelection loose = MediaInfo.Select([f1, f2, f3]);
            Assert.Equal(["z_video.mp4", "a_video.mp4", "m_video.mp4"], loose.Media.Select(Path.GetFileName));

            // Carpeta: se ordenan alfabéticamente a, m, z
            MediaSelection folder = MediaInfo.Select([dir]);
            Assert.Equal(["a_video.mp4", "m_video.mp4", "z_video.mp4"], folder.Media.Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Warning_formats_exact_messages_for_rejected_files_and_empty_folders()
    {
        MediaSelection singleRejected = new([], ["archivo.doc"], []);
        Assert.Equal("«archivo.doc» no es un video ni un audio que Subtitular reconozca (" + string.Join(", ", MediaInfo.Extensions) + ").",
            singleRejected.Warning);

        MediaSelection multiRejected = new([], ["a.txt", "b.bin"], []);
        Assert.Equal("«a.txt», «b.bin» no son un video ni un audio que Subtitular reconozca (" + string.Join(", ", MediaInfo.Extensions) + ").",
            multiRejected.Warning);

        MediaSelection singleEmpty = new([], [], ["C:\\vacia"]);
        Assert.Equal("La carpeta «vacia» no tiene videos ni audios.", singleEmpty.Warning);

        MediaSelection singleEmptyTrailingSlash = new([], [], [$"C:\\vacia{Path.DirectorySeparatorChar}"]);
        Assert.Equal("La carpeta «vacia» no tiene videos ni audios.", singleEmptyTrailingSlash.Warning);

        MediaSelection multiEmpty = new([], [], ["C:\\vacia1", "C:\\vacia2"]);
        Assert.Equal("Las carpetas «vacia1», «vacia2» no tienen videos ni audios.", multiEmpty.Warning);

        MediaSelection combined = new([], ["extra.txt"], ["C:\\vacia"]);
        Assert.Equal("«extra.txt» no es un video ni un audio que Subtitular reconozca (" + string.Join(", ", MediaInfo.Extensions) + ")."
            + " La carpeta «vacia» no tiene videos ni audios.", combined.Warning);

        MediaSelection none = new(["clip.mp4"], [], []);
        Assert.Null(none.Warning);
    }

    [Fact]
    public void Select_throws_on_null_argument()
    {
        Assert.Throws<ArgumentNullException>(() => MediaInfo.Select(null!));
    }
}
