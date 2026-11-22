"""Read-only end-to-end MCP connection check for the official Blender Lab bridge."""
import asyncio
import json
from pathlib import Path
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def main():
    python = Path(__file__).resolve().parents[1] / "blender_lab_mcp/.venv/Scripts/python.exe"
    params = StdioServerParameters(command=str(python), args=["-m", "blmcp"])
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            init = await session.initialize()
            listing = await session.list_tools()
            print(json.dumps({"server": init.serverInfo.model_dump(), "tool_count": len(listing.tools)}))
            reply = await session.call_tool("execute_blender_code", {
                "code": "import bpy\nresult = {'version': bpy.app.version_string, 'file': bpy.data.filepath, 'scene': bpy.context.scene.name, 'objects': len(bpy.context.scene.objects)}"
            })
            print(reply.model_dump_json())
            if reply.isError:
                raise RuntimeError("Blender MCP connection check failed")

asyncio.run(asyncio.wait_for(main(), timeout=30))
