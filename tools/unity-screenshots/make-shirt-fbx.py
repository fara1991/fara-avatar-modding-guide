import bpy
import sys

out = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.2, depth=0.6, location=(0, 0, 1.25))
obj = bpy.context.object
obj.name = "Shirt"
obj.data.name = "Shirt"
bpy.ops.object.shade_smooth()
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options="FBX_SCALE_ALL", object_types={"MESH"})
print("exported", out)
