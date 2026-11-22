using System.IO;
using System.Numerics;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

// The dev menu's Tools tab saves a model's scale and offset into models.json's text, its comments kept.
public class ModelCatalogTextTests
{
    private const string Catalog = """
        // the catalog's head comment, "id": "b" in a comment
        {
          "models": [
            {
              // a comment, "scale": 9
              "id": "a",
              "icons": [ "a.dmi" ],
              "rule": "single",
              "texture": "model",
              "parts": { "model": { "file": "a.glb", "offset": [ 1, 2, 3 ] } }
            },
            {
              "id": "b",
              "icons": [ "b.dmi" ],
              "rule": "single",
              "texture": "model",
              "scale": 0.8,
              "offset": [ 0, 0, 0.4 ],
              "parts": { "model": "b.glb" }
            }
          ]
        }
        """;

    [Fact]
    public void AnEntrysScaleAndOffsetAreSetInPlace()
    {
        string text = ModelCatalogText.SetPlacement(Catalog, "a", 1.25f, new Vector3(0.1f, 0f, -0.2f));
        ModelCatalog catalog = ModelCatalog.Parse(text);
        ModelEntry a = catalog.Entries[0], b = catalog.Entries[1];
        Assert.Equal((1.25f, new Vector3(0.1f, 0f, -0.2f)), (a.Scale, a.Offset));
        Assert.Equal((0.8f, new Vector3(0f, 0f, 0.4f)), (b.Scale, b.Offset)); // the other entry untouched
        Assert.Equal(new Vector3(1, 2, 3), a.Parts["model"].Offset); // a part's own offset is not the entry's
        Assert.Contains("// a comment, \"scale\": 9", text);
        Assert.Contains("      \"scale\": 1.25,\n", text.Replace("\r\n", "\n"));

        // rewritten in place; back to the defaults, an added one is rewritten too, not removed, and a property already at its value is left as written
        text = ModelCatalogText.SetPlacement(text, "b", 1.5f, Vector3.Zero);
        catalog = ModelCatalog.Parse(text);
        Assert.Equal((1.5f, Vector3.Zero), (catalog.Entries[1].Scale, catalog.Entries[1].Offset));
        Assert.Contains("\"offset\": [ 0, 0, 0 ],", text);
        text = ModelCatalogText.SetPlacement(text, "a", 1f, Vector3.Zero);
        Assert.Equal((1f, Vector3.Zero), (ModelCatalog.Parse(text).Entries[0].Scale, ModelCatalog.Parse(text).Entries[0].Offset));
        Assert.Equal(Catalog, ModelCatalogText.SetPlacement(Catalog, "b", 0.8f, new Vector3(0f, 0f, 0.4f)));
        Assert.Equal(Catalog, ModelCatalogText.SetPlacement(Catalog, "a", 1f, Vector3.Zero));
        Assert.Throws<InvalidDataException>(() => ModelCatalogText.SetPlacement(Catalog, "c", 2f, Vector3.Zero));
    }

    [Fact]
    public void EveryEntryOfTheRepositorysCatalogIsFoundAndKeptAsItIs()
    {
        string path = null;
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null && path == null; dir = dir.Parent)
            path = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName) is string p && File.Exists(p) ? p : null;
        Assert.NotNull(path);
        string text = File.ReadAllText(path);
        foreach (ModelEntry entry in ModelCatalog.Parse(text).Entries)
            Assert.Equal(text, ModelCatalogText.SetPlacement(text, entry.Id, entry.Scale, entry.Offset));
    }
}
