# Blender script: "Buddy" - basit, yuvarlak hatlı ragdoll karakteri üretir ve FBX olarak dışa aktarır.
#
# Kullanım (komut satırı):
#   blender -b -P make_buddy.py -- <cikti_klasoru>
#
# Karakter -Y yönüne bakar (Blender önden görünüm), Unity'de +Z'ye bakar.
# Her vücut parçası %100 tek bir kemiğe bağlıdır (rigid skin): ragdoll için ideal,
# eklem yerlerinde mesh esnemez, parçalar birbirinin içine girer (oyuncak bebek gibi).

import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

out_dir = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.dirname(__file__)

# --------------------------------------------------------------------- sahneyi temizle
bpy.ops.wm.read_factory_settings(use_empty=True)

# --------------------------------------------------------------------- iskelet
ARM_DIR = Vector((math.cos(math.radians(40)), 0, -math.sin(math.radians(40))))  # A-pose

def arm_points(side):
    d = Vector((ARM_DIR.x * side, 0, ARM_DIR.z))
    shoulder = Vector((0.21 * side, 0, 1.31))
    elbow = shoulder + d * 0.27
    wrist = elbow + d * 0.25
    tip = wrist + d * 0.11
    return shoulder, elbow, wrist, tip, d

BONES = [
    # isim,       head,               tail,               parent
    ("Hips",   (0, 0, 0.88),       (0, 0, 0.98),       None),
    ("Spine",  (0, 0, 0.98),       (0, 0, 1.12),       "Hips"),
    ("Chest",  (0, 0, 1.12),       (0, 0, 1.34),       "Spine"),
    ("Neck",   (0, 0, 1.34),       (0, 0, 1.42),       "Chest"),
    ("Head",   (0, 0, 1.42),       (0, 0, 1.74),       "Neck"),
]
for side, s in (("L", 1), ("R", -1)):
    x = 0.11 * s
    BONES += [
        (f"UpperLeg_{side}", (x, 0, 0.86), (x, 0, 0.48), "Hips"),
        (f"LowerLeg_{side}", (x, 0, 0.48), (x, 0, 0.11), f"UpperLeg_{side}"),
        (f"Foot_{side}",     (x, 0, 0.11), (x, -0.14, 0.03), f"LowerLeg_{side}"),
    ]
    sh, el, wr, tip, _ = arm_points(s)
    BONES += [
        (f"UpperArm_{side}", tuple(sh), tuple(el), "Chest"),
        (f"LowerArm_{side}", tuple(el), tuple(wr), f"UpperArm_{side}"),
        (f"Hand_{side}",     tuple(wr), tuple(tip), f"LowerArm_{side}"),
    ]

arm_data = bpy.data.armatures.new("BuddyArmature")
arm_obj = bpy.data.objects.new("Armature", arm_data)
bpy.context.scene.collection.objects.link(arm_obj)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode="EDIT")
for name, head, tail, parent in BONES:
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.roll = 0.0
    if parent:
        b.parent = arm_data.edit_bones[parent]
        b.use_connect = False
bpy.ops.object.mode_set(mode="OBJECT")

# --------------------------------------------------------------------- mesh parçaları
bm = bmesh.new()
groups = {}      # kemik adı -> [BMVert]
eye_faces = []   # göz materyali alacak yüzler

def add_sphere(bone, center, scale=(1, 1, 1), radius=1.0, stretch=0.0, rotation=Matrix.Identity(3), segs=(24, 12)):
    """UV küre üretir; stretch > 0 ise üst yarıyı uzatıp kapsül yapar (eksen +Z)."""
    res = bmesh.ops.create_uvsphere(bm, u_segments=segs[0], v_segments=segs[1], radius=radius)
    verts = res["verts"]
    for v in verts:
        co = v.co.copy()
        if stretch > 0 and co.z > 0:
            co.z += stretch
        co = Vector((co.x * scale[0], co.y * scale[1], co.z * scale[2]))
        v.co = rotation @ co + Vector(center)
    groups.setdefault(bone, []).extend(verts)
    return verts

def add_capsule(bone, a, b, radius):
    a, b = Vector(a), Vector(b)
    axis = b - a
    rot = axis.normalized().to_track_quat("Z", "Y").to_matrix()
    return add_sphere(bone, a, radius=radius, stretch=axis.length, rotation=rot)

# Gövde
add_sphere("Hips",  (0, 0, 0.90), scale=(0.21, 0.15, 0.13))
add_sphere("Spine", (0, 0, 1.04), scale=(0.205, 0.15, 0.16))
add_sphere("Chest", (0, 0, 1.21), scale=(0.22, 0.155, 0.19))
add_capsule("Neck", (0, 0, 1.30), (0, 0, 1.42), 0.065)
add_sphere("Head",  (0, 0, 1.57), scale=(0.17, 0.16, 0.18))
# Gözler (yön belli olsun diye)
for s in (1, -1):
    verts = add_sphere("Head", (0.055 * s, -0.152, 1.60), scale=(0.022, 0.012, 0.032), segs=(12, 6))
    vs = set(verts)
    eye_faces.extend({f for v in verts for f in v.link_faces if all(x in vs for x in f.verts)})

for side, s in (("L", 1), ("R", -1)):
    x = 0.11 * s
    add_capsule(f"UpperLeg_{side}", (x, 0, 0.84), (x, 0, 0.48), 0.095)
    add_capsule(f"LowerLeg_{side}", (x, 0, 0.48), (x, 0, 0.12), 0.08)
    add_sphere(f"Foot_{side}", (x, -0.05, 0.055), scale=(0.075, 0.13, 0.055))
    sh, el, wr, tip, d = arm_points(s)
    add_capsule(f"UpperArm_{side}", sh, el, 0.07)
    add_capsule(f"LowerArm_{side}", el, wr, 0.064)
    add_sphere(f"Hand_{side}", wr + d * 0.06, radius=0.075)

bm.verts.index_update()
eye_face_set = set(eye_faces)
for f in bm.faces:
    f.smooth = True
    f.material_index = 1 if f in eye_face_set else 0
index_groups = {k: [v.index for v in vs] for k, vs in groups.items()}

mesh = bpy.data.meshes.new("BuddyMesh")
bm.to_mesh(mesh)
bm.free()

body = bpy.data.objects.new("Body", mesh)
bpy.context.scene.collection.objects.link(body)

def make_material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    if m.node_tree is None and hasattr(m, "use_nodes"):
        m.use_nodes = True
    if m.node_tree:
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Roughness"].default_value = 0.6
    return m

mesh.materials.append(make_material("Buddy_Body", (0.93, 0.91, 0.87, 1)))
mesh.materials.append(make_material("Buddy_Eyes", (0.05, 0.05, 0.06, 1)))

for bone_name, idx in index_groups.items():
    vg = body.vertex_groups.new(name=bone_name)
    vg.add(idx, 1.0, "REPLACE")

body.parent = arm_obj
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = arm_obj

# --------------------------------------------------------------------- kaydet + dışa aktar
os.makedirs(out_dir, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "Buddy.blend"))

fbx_path = os.path.join(out_dir, "Buddy.fbx")
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=False,
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y",
    bake_space_transform=True,
    add_leaf_bones=False,
    armature_nodetype="NULL",
    bake_anim=False,
    mesh_smooth_type="FACE",
)
print("BUDDY_EXPORTED", fbx_path, "verts:", len(mesh.vertices))
