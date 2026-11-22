using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.Wave4.Contracts;

/// <summary>
/// The minimal <c>/image</c> (lead decision D3, W4.0; BYOND ref "image objects", <c>/proc/image</c>; OpenDream
/// DMStandard/Types/Image.dm and DreamObjectImage.Initialize): BYOND's defaults, the positional and named forms of
/// <c>image()</c> that tg's M0 callers use (prepare_huds, mob.dm:194; construct_radial_images,
/// _globalvars/lists/mobs.dm:101), the location shift, the appearance of an atom or an image, and a builtin value that
/// gets no world id.
/// </summary>
public class ImageTests
{
    [Fact]
    public void ImageVarsHaveByondsDefaults()
    {
        var made = image();
        Assert.Null(made.icon);
        Assert.Null(made.icon_state);
        Assert.Null(made.loc);
        Assert.Equal(FLOAT_LAYER, made.layer);
        Assert.Equal(FLOAT_PLANE, made.plane);
        Assert.Equal(SOUTH, made.dir);
        Assert.Equal(255f, made.alpha);
        Assert.Equal(0f, made.pixel_x);
        Assert.Equal(0f, made.pixel_y);
        Assert.Equal(0f, made.appearance_flags);
        Assert.Null(made.color);
        Assert.Equal(0f, made.invisibility);
        Assert.Equal(0f, made.@override);
        Assert.Equal("/image", DmTypes.PathOf(typeof(image)));
    }

    [Fact]
    public void PrepareHudsFormTakesTheIconTheLocationAndTheIconState()
    {
        var map = new EngineWorld(1, 1, 1);
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            // image(DEFAULT_HUDS_DMI, src, "") (mob.dm:194); the resource is the text of its path (RULE-GAP).
            var made = image("icons/mob/huds/hud.dmi", box, "");
            Assert.Equal("icons/mob/huds/hud.dmi", made.icon);
            Assert.Same(box, made.loc);
            Assert.Equal("", made.icon_state);
            Assert.Equal(FLOAT_LAYER, made.layer);
        }
    }

    [Fact]
    public void NamedArgumentsSkipTheLocation()
    {
        // image(icon = 'icons/mob/nonhuman-player/cult.dmi', icon_state = "juggernaut") (mobs.dm:102)
        var made = image(icon: "cult.dmi", icon_state: "juggernaut");
        Assert.Equal("cult.dmi", made.icon);
        Assert.Equal("juggernaut", made.icon_state);
        Assert.Null(made.loc);
    }

    [Fact]
    public void TextOrANumberInTheLocationPlaceIsTheIconStateAndTheRestShift()
    {
        // BYOND ref: "A text string will be interpreted as the icon_state argument if one is not present";
        // OpenDream: a second argument that is no object is the icon_state, the later ones shift down one place.
        var made = image("x.dmi", "state", 5, 4, 3, 2);
        Assert.Null(made.loc);
        Assert.Equal("state", made.icon_state);
        Assert.Equal(5f, made.layer);
        Assert.Equal(4f, made.dir);
        Assert.Equal(3f, made.pixel_x);
        Assert.Equal(2f, made.pixel_y);
    }

    [Fact]
    public void AnAtomOrAnImageGivesItsAppearanceAndLaterArgumentsOverrideIt()
    {
        var map = new EngineWorld(1, 1, 1);
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            box.icon = "box.dmi";
            box.icon_state = "closed";
            box.dir = EAST;
            box.alpha = 128;
            var made = image(box, null, "open");
            Assert.Equal("box.dmi", made.icon);
            Assert.Equal("open", made.icon_state);    // overridden
            Assert.Equal(OBJ_LAYER, made.layer);      // the atom's layer, not FLOAT_LAYER
            Assert.Equal(EAST, made.dir);
            Assert.Equal(128f, made.alpha);
            Assert.Null(made.loc);                    // the appearance's source is not the location

            var copy = image(made, box);
            Assert.Equal("open", copy.icon_state);
            Assert.Equal(128f, copy.alpha);
            Assert.Same(box, copy.loc);
        }
    }

    [Fact]
    public void WhatTgsM0CallersNeverPassIsARuntimeError()
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.Throws<DmRuntimeException>(() => image(5));                             // a number as the icon
            Assert.Throws<DmRuntimeException>(() => image("x.dmi", Dm.@new<datum>()));     // a datum that is no atom as the location
            Assert.Throws<DmRuntimeException>(() => image("x.dmi", null, 5));             // an icon_state that is no text
        }
    }

    [Fact]
    public void NewImageIsTheBuiltinAndGetsNoWorldId()
    {
        var w = new World();
        using (w.Enter())
        {
            // "The image() instruction is simply a short-hand for new/image()" (BYOND ref /proc/image).
            var made = Dm.@new<image>("x.dmi", null, "state");
            Assert.Equal("x.dmi", made.icon);
            Assert.Equal("state", made.icon_state);
            Assert.Equal(0, w.Objects.Count);
            Assert.Equal(1, w.AllocateId()); // no id was used
        }
    }
}
