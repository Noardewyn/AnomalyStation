"""Build and export the current original-atlas console revision through Blender MCP."""
from pathlib import Path
for filename in ('rebuild_console_bodies.py', 'export_rebuilt_consoles.py'):
    path = Path(__file__).with_name(filename)
    exec(compile(path.read_text(), str(path), 'exec'), {'__file__': str(path)})
