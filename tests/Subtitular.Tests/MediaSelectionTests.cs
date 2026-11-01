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
}
