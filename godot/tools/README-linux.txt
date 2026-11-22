Anomaly Station (Linux x64)
===========================

What's in this folder
 * The game:   AnomalyStation.x86_64   (right here). Start it and enter a server's address.
 * The editor: editor/AnomalyEditor.x86_64   (the map editor, see editor/README.txt).
Keep every folder where it is: the game and the editor read assets, maps and the data_... folders from here.
Nothing to install but a graphics driver with Vulkan: .NET is bundled.

If it does not start
 * "Permission denied" or nothing happens on a double-click: the unzip tool dropped the executable bit. In a terminal, in this folder:
     chmod +x AnomalyStation.x86_64 editor/AnomalyEditor.x86_64
 * The file manager opens it as text: run it from a terminal instead:   ./AnomalyStation.x86_64
 * "No Vulkan device" or a black window: install the Vulkan driver for your GPU (Mesa: mesa-vulkan-drivers / vulkan-radeon / vulkan-intel;
   NVIDIA: the proprietary driver), then check it with   vulkaninfo --summary
 * Anything else: the log is ~/.local/share/AnomalyStation/logs/godot.log - send it with your report.
