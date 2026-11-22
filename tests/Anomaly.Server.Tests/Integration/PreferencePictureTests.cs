using System.IO;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice D4 (lobby.md 8): the character page's choice pictures. The server sends each choice's picture as an /icon recipe in the
/// preferences' constant data (tg's icon_for and generate_icon_with_head_accessory, DV44: no spritesheet), and the client's icon
/// library composes it: a hairstyle is tg's cropped head, scaled back to 32 px, with the hair on it; the jumpsuit is its GAGS icon.
/// </summary>
public class PreferencePictureTests
{
    private static readonly bool HaveClone = TgStrings.Locate(AppContext.BaseDirectory, Directory.GetCurrentDirectory()) != null;

    private static (string Resource, string State) Of(DmList icons, string choice)
    {
        var entry = Assert.IsType<DmList>(icons[choice]);
        return ((string)entry[1], entry[2] as string);
    }

    private static int Opaque(IconLibrary library, (string Resource, string State) picture, out int width, out int height)
    {
        Assert.True(library.TryGetIcon(picture.Resource, out IIcon icon), "no icon for " + picture.Resource);
        Assert.True(icon.TryGetShown(picture.State, out IIconState state), "no state " + picture.State);
        width = state.Width;
        height = state.Height;
        Raster raster = state.Get(0, 0);
        int opaque = 0;
        for (int i = 3; i < raster.Pixels.Length; i += 4)
            if (raster.Pixels[i] != 0)
                opaque++;
        return opaque;
    }

    [Fact]
    public void TheConstantDataHoldsEachChoicesRecipeAndTheClientDrawsIt()
    {
        if (!HaveClone)
            return; // the tgstation clone holds tg's name lists

        using var rig = new BotServerRig(strict: true);
        DmList hair, underwear, jumpsuit;
        int values = 0;
        using (rig.World.Enter())
        {
            DmList Icons(Type type) => (DmList)((DmList)((datum_preference)GLOB.preference_entries[type]).compile_constant_data())["icons"];
            hair = Icons(typeof(datum_preference_choiced_hairstyle));
            underwear = Icons(typeof(datum_preference_choiced_underwear));
            jumpsuit = Icons(typeof(datum_preference_choiced_jumpsuit));
            foreach (Type type in new[] { typeof(datum_preference_choiced_hairstyle), typeof(datum_preference_choiced_facial_hairstyle), typeof(datum_preference_choiced_socks),
                typeof(datum_preference_choiced_undershirt), typeof(datum_preference_choiced_underwear), typeof(datum_preference_choiced_backpack), typeof(datum_preference_choiced_jumpsuit) })
            {
                DmList icons = Icons(type);
                values += (int)icons.len * 3;
                foreach (object choice in Dm.each(icons))
                    Assert.True(Of(icons, (string)choice).Resource.Length <= Limits.MaxUiStringBytes, type.Name + " " + choice);
            }
        }
        Assert.True(values < Limits.MaxUiValues / 4, values + " values"); // the window's data stays well within its limits
        Assert.StartsWith("iconops:", Of(hair, "Braid (Floorlength)").Resource);
        Assert.StartsWith("gags:", Of(jumpsuit, "Jumpsuit").Resource);

        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var library = new IconLibrary(new DirectoryIconFiles(root));
        int bald = Opaque(library, Of(hair, "Bald"), out int width, out int height);
        Assert.Equal((32, 32), (width, height)); // tg's crop of the head, scaled back to 32 px
        Assert.True(bald > 32 * 8, "the head fills the middle of the picture: " + bald);
        int braided = Opaque(library, Of(hair, "Braid (Floorlength)"), out _, out _);
        Assert.True(braided > bald, $"bald {bald}, braided {braided}");
        Assert.True(Opaque(library, Of(underwear, "Briefs"), out width, out height) > 0);
        Assert.Equal((32, 32), (width, height));
        Assert.True(Opaque(library, Of(jumpsuit, "Jumpsuit"), out _, out _) > 0);
    }

    // Syndicate A3 (D316): an antagonist's preference picture (tg's get_preview_icon: the dummy in its outfit flattened, scaled to 115 px,
    // shifted 8 west and 30 south and cropped to 96 px), composed by the client's icon library with the recipe's Shift
    [Fact]
    public void An_antagonists_picture_is_its_outfit_flattened_and_cropped_to_96_pixels()
    {
        using var rig = new BotServerRig(strict: true);
        string resource;
        using (rig.World.Enter())
        {
            var antag = Dm.@new<datum_antagonist_custom>();
            antag.preview_outfit = typeof(datum_outfit_job_assistant);
            resource = (Anomaly.Core.Kernel.Waiting.NoSleep(antag.get_preview_icon()) as icon).dm_resource();
        }
        Assert.True(resource.Length <= Limits.MaxUiStringBytes, resource.Length + " bytes");
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var library = new IconLibrary(new DirectoryIconFiles(root));
        int opaque = Opaque(library, (resource, "nothing"), out int width, out int height);
        Assert.Equal((96, 96), (width, height));
        Assert.True(opaque > 96 * 24, "the head and chest fill a third of the picture: " + opaque);
    }
}
