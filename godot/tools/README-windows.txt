Anomaly Station (Windows x64)
=============================

What's in this folder
 * The game:   AnomalyStation.exe   (right here). Start it and enter a server's address.
 * The editor: editor\AnomalyEditor.exe   (the map editor, see editor\README.txt).
Keep every folder where it is: the game and the editor read assets, maps and the data_... folders from here.
Nothing to install: .NET is bundled.

If it does not start
 * You started it from inside the zip: right-click the zip > Extract All, then run the exe from the extracted folder.
 * "Windows protected your PC" (SmartScreen): click "More info" > "Run anyway". The build is not signed.
 * The antivirus deleted or blocked a file: allow the folder in the antivirus and extract the zip again.
 * The window flashes and closes, or says a file is missing: extract the whole zip again, into a short path such as C:\AnomalyStation.
 * Black screen, crash at start, or graphics errors: update the graphics driver. If that does not help, try the other renderer:
     AnomalyStation.exe --rendering-driver vulkan
 * Anything else: the log is %APPDATA%\AnomalyStation\logs\godot.log - send it with your report.
