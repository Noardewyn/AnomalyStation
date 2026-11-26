namespace Anomaly.Protocol;

// Protocol version 78 (silicons SI4, MessagesV78.cs). In the private block's version 43 flags byte:
// - bit 2 (4): the camera chunks, [count] {[varuint x] [varuint y] [varuint z] [u64 obscured]}*;
// - bit 3 (8): the popup windows, [count] {[string name] [string title] [varuint width] [varuint height]}*;
// - bit 4 (16): the command verbs, [count] {[string category] [string name] [string path]}*;
// - bit 5 (32): the view areas, [count] {[varuint screen id] [string map] [varuint x1] [varuint y1] [varuint x2] [varuint y2] [varuint z]}*;
// each after the eye, in that order. The actions Verb (22) and PopupClose (23): [string text].
public static partial class Codec
{
    private const byte OwnV78Static = 4;
    private const byte OwnV78Popups = 8;
    private const byte OwnV78Verbs = 16;
    private const byte OwnV78Areas = 32;
    private const byte OwnV89HandsBlocked = 64; // version 89 (MessagesV89.cs): no bytes of its own
    private const byte OwnV95Cooldown = 128; // version 95 (MessagesV95.cs): [varuint milliseconds] after the view areas
    private const byte OwnV43FlagsAll = OwnV43Access | OwnV51Eye | OwnV78Static | OwnV78Popups | OwnV78Verbs | OwnV78Areas | OwnV89HandsBlocked | OwnV95Cooldown;

    private static byte OwnV78Flags(OwnBlock own)
        => (byte)((own.StaticChunks != null ? OwnV78Static : 0) | (own.Popups != null ? OwnV78Popups : 0) | (own.Verbs != null ? OwnV78Verbs : 0)
            | (own.ViewAreas != null ? OwnV78Areas : 0) | (own.HandsBlocked ? OwnV89HandsBlocked : 0)
            | (own.MeleeCooldownMs != OwnBlock.DefaultMeleeCooldownMs ? OwnV95Cooldown : 0));

    private static void WriteOwnV78(ref WireWriter w, OwnBlock own)
    {
        if (own.StaticChunks != null)
        {
            w.WriteCount(own.StaticChunks.Count, Limits.MaxStaticChunks);
            foreach (StaticChunk chunk in own.StaticChunks)
            {
                w.WriteVarUInt(CheckCoordinate(chunk.X, Limits.MaxMapSize, "a static chunk's x"));
                w.WriteVarUInt(CheckCoordinate(chunk.Y, Limits.MaxMapSize, "a static chunk's y"));
                w.WriteVarUInt(CheckCoordinate(chunk.Z, Limits.MaxMapLevels, "a static chunk's z"));
                w.WriteUInt64(chunk.Obscured);
            }
        }

        if (own.Popups != null)
        {
            w.WriteCount(own.Popups.Count, Limits.MaxPopups);
            foreach (PopupWindow popup in own.Popups)
            {
                if (popup == null || string.IsNullOrEmpty(popup.Name) || popup.Width < 1 || popup.Height < 1 || popup.Width > Limits.MaxPopupSide || popup.Height > Limits.MaxPopupSide)
                    throw Bad("a popup window with no name or a size out of range");
                w.WriteString(popup.Name, Limits.MaxPopupNameBytes);
                w.WriteString(popup.Title ?? "", Limits.MaxPopupTitleBytes);
                w.WriteVarUInt((ulong)popup.Width);
                w.WriteVarUInt((ulong)popup.Height);
            }
        }

        if (own.Verbs != null)
        {
            w.WriteCount(own.Verbs.Count, Limits.MaxVerbs);
            foreach (VerbEntry verb in own.Verbs)
            {
                if (verb == null || string.IsNullOrEmpty(verb.Category) || string.IsNullOrEmpty(verb.Name) || string.IsNullOrEmpty(verb.Path))
                    throw Bad("a verb with no category, name or path");
                w.WriteString(verb.Category, Limits.MaxVerbNameBytes);
                w.WriteString(verb.Name, Limits.MaxVerbNameBytes);
                w.WriteString(verb.Path, Limits.MaxVerbPathBytes);
            }
        }

        if (own.ViewAreas != null)
        {
            w.WriteCount(own.ViewAreas.Count, Limits.MaxViewAreas);
            foreach (ViewArea area in own.ViewAreas)
            {
                if (area.ScreenId < 0 || (area.ScreenId == 0) == string.IsNullOrEmpty(area.Map) || area.X1 > area.X2 || area.Y1 > area.Y2)
                    throw Bad("a view area of no screen object or map, or of both, or an empty rectangle");
                w.WriteVarUInt((ulong)area.ScreenId);
                w.WriteString(area.Map ?? "", Limits.MaxPopupNameBytes + 4);
                w.WriteVarUInt(CheckCoordinate(area.X1, Limits.MaxMapSize, "a view area's x1"));
                w.WriteVarUInt(CheckCoordinate(area.Y1, Limits.MaxMapSize, "a view area's y1"));
                w.WriteVarUInt(CheckCoordinate(area.X2, Limits.MaxMapSize, "a view area's x2"));
                w.WriteVarUInt(CheckCoordinate(area.Y2, Limits.MaxMapSize, "a view area's y2"));
                w.WriteVarUInt(CheckCoordinate(area.Z, Limits.MaxMapLevels, "a view area's z"));
            }
        }

        if (own.MeleeCooldownMs != OwnBlock.DefaultMeleeCooldownMs) // version 95
        {
            if (own.MeleeCooldownMs < 0 || own.MeleeCooldownMs > OwnBlock.MaxMeleeCooldownMs)
                throw Bad("a melee cooldown out of range");
            w.WriteVarUInt((ulong)own.MeleeCooldownMs);
        }
    }

    private static void ReadOwnV78(ref WireReader r, OwnBlock own, byte flags)
    {
        if ((flags & OwnV78Static) != 0)
        {
            int count = r.ReadCount(Limits.MaxStaticChunks, 1 + 1 + 1 + 8);
            own.StaticChunks = new List<StaticChunk>(count);
            for (int i = 0; i < count; i++)
            {
                int x = ReadRanged(ref r, 1, Limits.MaxMapSize, "a static chunk's x");
                int y = ReadRanged(ref r, 1, Limits.MaxMapSize, "a static chunk's y");
                int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a static chunk's z");
                own.StaticChunks.Add(new StaticChunk(x, y, z, r.ReadUInt64()));
            }
        }

        if ((flags & OwnV78Popups) != 0)
        {
            int count = r.ReadCount(Limits.MaxPopups, 1 + 1 + 1 + 1);
            own.Popups = new List<PopupWindow>(count);
            for (int i = 0; i < count; i++)
            {
                string name = r.ReadString(Limits.MaxPopupNameBytes);
                string title = r.ReadString(Limits.MaxPopupTitleBytes) ?? "";
                ulong width = r.ReadVarUInt();
                ulong height = r.ReadVarUInt();
                if (string.IsNullOrEmpty(name) || width < 1 || height < 1 || width > Limits.MaxPopupSide || height > Limits.MaxPopupSide)
                    throw Bad("a popup window with no name or a size out of range");
                own.Popups.Add(new PopupWindow(name, title, (int)width, (int)height));
            }
        }

        if ((flags & OwnV78Verbs) != 0)
        {
            int count = r.ReadCount(Limits.MaxVerbs, 1 + 1 + 1);
            own.Verbs = new List<VerbEntry>(count);
            for (int i = 0; i < count; i++)
            {
                string category = r.ReadString(Limits.MaxVerbNameBytes);
                string name = r.ReadString(Limits.MaxVerbNameBytes);
                string path = r.ReadString(Limits.MaxVerbPathBytes);
                if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                    throw Bad("a verb with no category, name or path");
                own.Verbs.Add(new VerbEntry(category, name, path));
            }
        }

        if ((flags & OwnV78Areas) != 0)
        {
            int count = r.ReadCount(Limits.MaxViewAreas, 1 + 1 + 5);
            own.ViewAreas = new List<ViewArea>(count);
            for (int i = 0; i < count; i++)
            {
                long screen = ReadNonNegativeLong(ref r, "a view area's screen object");
                string map = r.ReadString(Limits.MaxPopupNameBytes + 4) ?? "";
                int x1 = ReadRanged(ref r, 1, Limits.MaxMapSize, "a view area's x1");
                int y1 = ReadRanged(ref r, 1, Limits.MaxMapSize, "a view area's y1");
                int x2 = ReadRanged(ref r, 1, Limits.MaxMapSize, "a view area's x2");
                int y2 = ReadRanged(ref r, 1, Limits.MaxMapSize, "a view area's y2");
                int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a view area's z");
                if ((screen == 0) == (map.Length == 0) || x1 > x2 || y1 > y2)
                    throw Bad("a view area of no screen object or map, or of both, or an empty rectangle");
                own.ViewAreas.Add(new ViewArea(screen, map.Length == 0 ? null : map, x1, y1, x2, y2, z));
            }
        }

        own.MeleeCooldownMs = (flags & OwnV95Cooldown) != 0 // version 95
            ? ReadRanged(ref r, 0, OwnBlock.MaxMeleeCooldownMs, "a melee cooldown")
            : OwnBlock.DefaultMeleeCooldownMs;
    }

    private static long MaxOwnV78Bytes(OwnBlock own)
        => (own.StaticChunks == null ? 0 : 5 + (long)own.StaticChunks.Count * (5 + 5 + 5 + 8))
            + (own.Popups == null ? 0 : 5 + (long)own.Popups.Count * (5 + Limits.MaxPopupNameBytes + 5 + Limits.MaxPopupTitleBytes + 5 + 5))
            + (own.Verbs == null ? 0 : 5 + (long)own.Verbs.Count * (5 + Limits.MaxVerbNameBytes + 5 + Limits.MaxVerbNameBytes + 5 + Limits.MaxVerbPathBytes))
            + (own.ViewAreas == null ? 0 : 5 + (long)own.ViewAreas.Count * (10 + 5 + Limits.MaxPopupNameBytes + 4 + 5 * 5))
            + 5; // version 95: the melee cooldown
}
