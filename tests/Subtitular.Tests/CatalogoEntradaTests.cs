using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rodaje.Packages.Download;

namespace Subtitular.Tests;

/// <summary>Corre <c>build\catalogo-entrada.ps1</c> sobre un zip de prueba: la huella y el tamaño deben ser los reales y la entrada debe pasar por <see cref="SignedIndex"/>.</summary>
public sealed class CatalogoEntradaTests
{
    private static string FindRepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    [Fact]
    public void Catalogo_entrada_tiene_la_huella_real_del_zip_y_pasa_por_SignedIndex()
    {
        string root = FindRepoRoot();
        string temp = Directory.CreateTempSubdirectory("subtitular-catalogo-").FullName;
        try
        {
            string zipPath = Path.Combine(temp, "Subtitular-9.9.9-win-x64.zip");
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            using (Stream contenido = zip.CreateEntry("README.txt").Open())
            {
                contenido.Write("prueba"u8);
            }
            string jsonPath = Path.Combine(temp, "catalogo.json");

            var psi = new ProcessStartInfo("powershell") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(Path.Combine(root, "build", "catalogo-entrada.ps1"));
            psi.ArgumentList.Add("-Version");
            psi.ArgumentList.Add("9.9.9");
            psi.ArgumentList.Add("-Zip");
            psi.ArgumentList.Add(zipPath);
            psi.ArgumentList.Add("-Salida");
            psi.ArgumentList.Add(jsonPath);
            using Process proceso = Process.Start(psi)!;
            string salida = proceso.StandardOutput.ReadToEnd() + proceso.StandardError.ReadToEnd();
            proceso.WaitForExit();
            Assert.True(proceso.ExitCode == 0, salida);

            string expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zipPath)));
            long expectedSize = new FileInfo(zipPath).Length;
            string jsonContent = File.ReadAllText(jsonPath, Encoding.UTF8);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            List<string> modulos = [.. doc.RootElement.GetProperty("modulos").EnumerateArray().Select(e => e.GetString()!)];
            Assert.Equal(["modulo-subtitulos", "modulo-traducir", "modulo-voz"], modulos);

            // La entrada debe entrar tal cual en el índice firmado que leen Rodaje y el Lanzador.
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using ECDsa pub = IndexSignature.TryImportPublicKey(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()))!;
            byte[] indexBytes = Encoding.UTF8.GetBytes($$"""{ "formatVersion": 1, "generated": "2026-10-24T12:00:00Z", "entries": [ {{jsonContent}} ] }""");
            var problems = new List<string>();
            SignedIndex index = SignedIndex.Load(indexBytes, IndexSignature.Sign(indexBytes, key), pub, DownloadUrlPolicy.Default, problems);

            Assert.Empty(problems);
            IndexEntry entry = Assert.Single(index.Entries);
            Assert.Equal("subtitular", entry.Id);
            Assert.Equal(IndexEntryKind.App, entry.Kind);
            Assert.Equal("9.9.9", entry.Version.ToString());
            Assert.Equal(expectedSha256, entry.Sha256);
            Assert.Equal(expectedSize, entry.Size);
            Assert.Equal(new Uri("https://github.com/Dathinel/subtitular/releases/download/v9.9.9/Subtitular-9.9.9-win-x64.zip"), entry.Url);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
