Anomaly Station (macOS, Apple silicon and Intel)
================================================

What's in this folder
 * The game:   Anomaly Station.app   (right here). Start it and enter a server's address.
 * The editor: editor/Anomaly Editor.app   (the map editor, see editor/README.txt).
Keep every folder where it is: the game and the editor read assets and maps from beside the app.
Nothing to install: .NET is bundled.

If it does not start
 * "Anomaly Station is damaged and can't be opened" or "cannot be opened because Apple cannot check it": the app is not notarized,
   and macOS blocks apps downloaded from the internet. Clear the block once for the whole folder, in Terminal:
     xattr -dr com.apple.quarantine /path/to/this/folder
   (type "xattr -dr com.apple.quarantine " and drag this folder into the Terminal window to fill in the path).
   Or: try to open the app, then System Settings > Privacy & Security > "Open Anyway" (do it for the editor too).
 * It opens but finds no maps or textures: the app was moved out of this folder (for example into Applications). Keep it here.
 * Anything else: the log is ~/Library/Application Support/AnomalyStation/logs/godot.log - send it with your report.
