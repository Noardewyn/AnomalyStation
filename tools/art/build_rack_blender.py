"""Compatibility entry point for the current closed, textured rack builder."""
from pathlib import Path
import bpy

path=Path(__file__).with_name('rebuild_storage_rack.py')
namespace={'__file__':str(path)}
exec(compile(path.read_text(),str(path),'exec'),namespace)
namespace['export']()
