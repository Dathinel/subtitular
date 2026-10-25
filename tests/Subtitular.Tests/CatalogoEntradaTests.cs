using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rodaje.Packages.Download;

namespace Subtitular.Tests;

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
    public void Catalogo_entrada_cumple_requisitos_de_SignedIndex_y_coincide_con_zip()
    {
        string root = FindRepoRoot();
        string catalogoJsonPath = Path.Combine(root, "dist", "catalogo-subtitular.json");
        string zipPath = Path.Combine(root, "dist", "Subtitular-1.0.0-win-x64.zip");

        Assert.True(File.Exists(catalogoJsonPath), $"Falta {catalogoJsonPath}; corre build\\distribuir.ps1 antes.");
        Assert.True(File.Exists(zipPath), $"Falta {zipPath}; corre build\\distribuir.ps1 antes.");

        byte[] zipBytes = File.ReadAllBytes(zipPath);
        string expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(zipBytes));
        long expectedSize = new FileInfo(zipPath).Length;

        string jsonContent = File.ReadAllText(catalogoJsonPath, Encoding.UTF8);
        using JsonDocument doc = JsonDocument.Parse(jsonContent);
        JsonElement rootEl = doc.RootElement;

        // Comprobar campos exigidos por SignedIndex
        Assert.True(rootEl.TryGetProperty("id", out JsonElement idProp));
        Assert.Equal("subtitular", idProp.GetString());

        Assert.True(rootEl.TryGetProperty("version", out JsonElement versionProp));
        Assert.Equal("1.0.0", versionProp.GetString());

        Assert.True(rootEl.TryGetProperty("kind", out JsonElement kindProp));
        Assert.Equal("app", kindProp.GetString());

        Assert.True(rootEl.TryGetProperty("url", out JsonElement urlProp));
        string url = urlProp.GetString()!;
        Assert.Equal("https://github.com/Dathinel/subtitular/releases/download/v1.0.0/Subtitular-1.0.0-win-x64.zip", url);

        Assert.True(rootEl.TryGetProperty("sha256", out JsonElement shaProp));
        string sha256 = shaProp.GetString()!;
        Assert.Equal(expectedSha256, sha256);

        Assert.True(rootEl.TryGetProperty("size", out JsonElement sizeProp));
        long size = sizeProp.GetInt64();
        Assert.Equal(expectedSize, size);

        // Comprobar módulos declarados
        Assert.True(rootEl.TryGetProperty("modulos", out JsonElement modulosProp));
        Assert.Equal(JsonValueKind.Array, modulosProp.ValueKind);
        List<string> modulos = [.. modulosProp.EnumerateArray().Select(e => e.GetString()!)];
        Assert.Contains("modulo-subtitulos", modulos);
        Assert.Contains("modulo-traducir", modulos);
        Assert.Contains("modulo-voz", modulos);

        // Verificar parseo y validación de SignedIndex
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pubBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        using ECDsa pub = IndexSignature.TryImportPublicKey(pubBase64)!;

        string indexJson = $$"""
        {
          "formatVersion": 1,
          "generated": "2026-10-24T12:00:00Z",
          "entries": [
            {{jsonContent}}
          ]
        }
        """;

        byte[] indexBytes = Encoding.UTF8.GetBytes(indexJson);
        byte[] signature = IndexSignature.Sign(indexBytes, key);

        var problems = new List<string>();
        SignedIndex index = SignedIndex.Load(indexBytes, signature, pub, DownloadUrlPolicy.Default, problems);

        Assert.Empty(problems);
        Assert.Single(index.Entries);

        IndexEntry entry = index.Entries[0];
        Assert.Equal("subtitular", entry.Id);
        Assert.Equal(IndexEntryKind.App, entry.Kind);
        Assert.Equal(expectedSha256, entry.Sha256);
        Assert.Equal(expectedSize, entry.Size);
        Assert.Equal(new Uri(url), entry.Url);
    }
}
