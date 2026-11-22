using System.IO;
using System.Text;
using System.Text.Json;

namespace Anomaly.Tools.Icons;

// Owner: M2 P7. The .json next to each converted icon: what the client's IconAtlas (godot/scripts/Shared/Icons/IconSheet.cs)
// reads. The schema is part of the tool's contract with the client; both sides are tested against the same text.
//
//   { "version": "4.0", "width": 32, "height": 32, "sheetWidth": 512, "sheetHeight": 256,
//     "states": [ { "name": "floor", "dirs": 1, "frames": 1, "delays": null, "loop": 0, "rewind": false, "movement": false,
//                   "hotspot": null, "first": 0, "rects": [[0, 0, 32, 32]] } ] }
//
// rects has dirs * frames entries, direction-major (the frame of direction d, animation frame f is rects[d * frames + f]), each
// the x, y, width, height of the frame in the PNG (y from the top); first is the index of the state's first frame in the sheet. The
// sheet itself is frame-major (Dmi.cs): direction d of frame f is sheet frame first + f * dirs + d.
// delays are in deciseconds, one per frame (null for a still state).

/// <summary>Writes a <see cref="DmiFile"/> as the client's JSON.</summary>
public static class DmiJson
{
    /// <summary>The JSON text of <paramref name="file"/>; throws <see cref="InvalidDataException"/> when a state does not fit the sheet.</summary>
    public static string Write(DmiFile file)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("version", file.Version);
            json.WriteNumber("width", file.Width);
            json.WriteNumber("height", file.Height);
            json.WriteNumber("sheetWidth", file.SheetWidth);
            json.WriteNumber("sheetHeight", file.SheetHeight);
            json.WriteStartArray("states");
            foreach (DmiState state in file.States)
            {
                int count = state.Dirs * state.Frames;
                if (state.First + count > file.Capacity)
                    throw new InvalidDataException($"state '{state.Name}' needs frames {state.First}..{state.First + count - 1} but the sheet holds {file.Capacity}");
                json.WriteStartObject();
                json.WriteString("name", state.Name);
                json.WriteNumber("dirs", state.Dirs);
                json.WriteNumber("frames", state.Frames);
                if (state.Delays == null)
                    json.WriteNull("delays");
                else
                {
                    json.WriteStartArray("delays");
                    foreach (double delay in state.Delays)
                        json.WriteNumberValue(delay);
                    json.WriteEndArray();
                }
                json.WriteNumber("loop", state.Loop);
                json.WriteBoolean("rewind", state.Rewind);
                json.WriteBoolean("movement", state.Movement);
                if (state.Hotspot == null)
                    json.WriteNull("hotspot");
                else
                    json.WriteString("hotspot", state.Hotspot);
                json.WriteNumber("first", state.First);
                json.WriteStartArray("rects");
                for (int i = 0; i < count; i++)
                {
                    int dir = i / state.Frames, frame = i % state.Frames;
                    (int x, int y) = file.FramePosition(state.First + frame * state.Dirs + dir);
                    json.WriteStartArray();
                    json.WriteNumberValue(x);
                    json.WriteNumberValue(y);
                    json.WriteNumberValue(file.Width);
                    json.WriteNumberValue(file.Height);
                    json.WriteEndArray();
                }
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
