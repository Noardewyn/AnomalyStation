using Anomaly.Client.Shared.World;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared;

// Owner: DV586 (the owner, 2026-11-21: a swing of a knife or a sword was silent). Engine-free.
// tg has no swing sound: an item sounds only when it hits (its hitsound, item_attack.dm:244-246), and a click on the floor or the air with it does
// nothing at all. The port's own swing of the held thing on each click (D285) whooshes with tg's own whooshes: a thrown thing's
// (living_item_handling.dm:102-105), tg's unarmed miss (punchmiss) and the cain and abel dagger's slashes.

/// <summary>What the own swing whooshes like.</summary>
public enum SwingKind : byte
{
    /// <summary>An empty hand: tg's unarmed miss.</summary>
    Fist,

    /// <summary>Anything else: a thing thrown softly.</summary>
    Light,

    /// <summary>A long or a bulky thing: a thing thrown hard.</summary>
    Heavy,

    /// <summary>An edged thing (tg's sharpness): the dagger's slashes, one and the other in turn.</summary>
    Sharp,

    /// <summary>A pointed thing (tg's SHARP_POINTY: a screwdriver, a spear, a pen): it whooshes as a sharp one and stabs (HeldClip.Stab).</summary>
    Pointy,
}

/// <summary>The own swing's whoosh: its file and volume, and which held pictures are tg's sharp or bulky things.</summary>
public static class SwingSound
{
    /// <summary>A whoosh to play: its file and tg's volume (0 to 100).</summary>
    public readonly record struct Whoosh(string File, float Volume);

    public const string Fist = "sound/items/weapons/punchmiss.ogg", Soft = "sound/items/weapons/throwsoft.ogg", Hard = "sound/items/weapons/throwhard.ogg";
    public const string SlashOne = "sound/items/weapons/cain_and_abel/dagger_slash_1.ogg", SlashTwo = "sound/items/weapons/cain_and_abel/dagger_slash_2.ogg";

    /// <summary>tg's <c>WEIGHT_CLASS_BULKY</c>: a thing this big or bigger swings heavy.</summary>
    public const int Bulky = 4;

    /// <summary>The whoosh of the <paramref name="count"/>th swing (the sharp ones take turns).</summary>
    public static string FileOf(SwingKind kind, int count) => kind switch
    {
        SwingKind.Sharp or SwingKind.Pointy => count % 2 == 0 ? SlashOne : SlashTwo,
        SwingKind.Heavy => Hard,
        SwingKind.Fist => Fist,
        _ => Soft,
    };

    /// <summary>tg's volume (0 to 100): about tg's own whooshes' (punchmiss at 25), quieter than the hit that may follow.</summary>
    public static float VolumeOf(SwingKind kind) => kind == SwingKind.Heavy ? 35f : 25f;

    /// <summary>The kind of a held thing: its picture's facts if tg has them (<paramref name="looks"/>), and whether its picture is a long one.</summary>
    public static SwingKind KindOf(IReadOnlyDictionary<(string Icon, string State), SwingKind> looks, string icon, string state, bool longThing)
    {
        if (icon == null)
            return SwingKind.Fist;
        SwingKind kind = looks != null && looks.TryGetValue((PictureOf(icon), state ?? string.Empty), out SwingKind known) ? known : SwingKind.Light;
        return kind == SwingKind.Light && longThing ? SwingKind.Heavy : kind;
    }

    // a greyscale (GAGS) icon without its colours: a screwdriver's are random, so its picture is any colour of its config's
    private static string PictureOf(string icon)
        => icon.StartsWith("gags:", StringComparison.Ordinal) && icon.IndexOf('|') is > 0 and int bar ? icon[..bar] : icon;

    /// <summary>
    /// tg's items by their picture (icon and the state they draw after init): a pointed thing's is <see cref="SwingKind.Pointy"/>, an edged one's
    /// <see cref="SwingKind.Sharp"/>, a bulky one's <see cref="SwingKind.Heavy"/> (a picture several types share takes the latest of these kinds any of them has). Read from the translated classes: a second or two.
    /// </summary>
    public static Dictionary<(string Icon, string State), SwingKind> Looks(ITypeSource types)
    {
        var looks = new Dictionary<(string, string), SwingKind>();
        foreach (string path in TypeOrder.Paths)
        {
            if (!path.StartsWith("/obj/item/", StringComparison.Ordinal) || !types.TryGet(path, out TypeDefaults d) || d.Icon == null)
                continue;
            SwingKind kind = d.Pointy ? SwingKind.Pointy : d.Sharp ? SwingKind.Sharp : d.WeightClass >= Bulky ? SwingKind.Heavy : SwingKind.Light;
            if (kind == SwingKind.Light)
                continue;
            string icon = PictureOf(d.IconAfterInit(null)), state = d.StateAfterInit(null) ?? string.Empty;
            Add((icon, state), kind);
            // tg's two_handed things draw "<base>0" and "<base>1" when wielded (a spear's spearglass0 and spearglass1): the wielded picture is the same thing
            if (state.EndsWith('0'))
                Add((icon, state[..^1] + "1"), kind);
        }
        return looks;

        void Add((string, string) key, SwingKind kind)
        {
            if (!looks.TryGetValue(key, out SwingKind had) || kind > had)
                looks[key] = kind;
        }
    }
}
