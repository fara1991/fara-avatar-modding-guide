"""記事用スクリーンショットを Blender の中から自動で撮る。

    blender --window-geometry 0 0 1600 900 --python tools/screenshots/capture.py -- --out Content/learn --work %TEMP%/shots [--only name ...]

- 各ショットは @shot で登録した関数。シーンを組み立てたあと、指定したエリア（またはウィンドウ全体）を撮る。
- 撮った画像は <out>/<category>/images/<name>.png に保存される。
- 切り抜きや番号付けが必要なものは <work>/post.json に書き出し、postprocess.py が処理する。
- 日本語 UI・スプラッシュなしの設定は capture.ps1 が用意する（BLENDER_USER_CONFIG）。
"""
import ctypes
import json
import os
import sys
import traceback
from math import radians

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

# ---------------------------------------------------------------- 引数
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = None
WORK = None
ONLY = []
i = 0
while i < len(argv):
    if argv[i] == "--out":
        OUT = argv[i + 1]
        i += 2
    elif argv[i] == "--work":
        WORK = argv[i + 1]
        i += 2
    elif argv[i] == "--only":
        i += 1
        while i < len(argv) and not argv[i].startswith("--"):
            ONLY.append(argv[i])
            i += 1
    else:
        i += 1
if not OUT or not WORK:
    raise SystemExit("usage: ... -- --out <Content/learn> --work <dir> [--only name ...]")
os.makedirs(WORK, exist_ok=True)
LOG = os.path.join(WORK, "capture.log")
POST = []


def log(msg):
    with open(LOG, "a", encoding="utf-8") as f:
        f.write(msg + "\n")


# ---------------------------------------------------------------- ウィンドウ・エリア
def win():
    return bpy.context.window_manager.windows[0]


def area_of(kind, w=None):
    w = w or win()
    return next(a for a in w.screen.areas if a.type == kind)


def region_of(area, kind="WINDOW"):
    return next(r for r in area.regions if r.type == kind)


class ctx:
    """3D ビューポート（または指定エリア）を対象にした temp_override"""

    def __init__(self, area_type="VIEW_3D", w=None, region="WINDOW"):
        self.w = w or win()
        self.area = area_of(area_type, self.w)
        self.region = region_of(self.area, region)

    def __enter__(self):
        self.o = bpy.context.temp_override(window=self.w, area=self.area, region=self.region)
        self.o.__enter__()
        return self

    def __exit__(self, *a):
        self.o.__exit__(*a)


def press_esc():
    """開いたメニューを閉じる（Windows のキー入力をエミュレート）"""
    ctypes.windll.user32.keybd_event(0x1B, 0, 0, 0)
    ctypes.windll.user32.keybd_event(0x1B, 0, 2, 0)


def warp_to_center(area_type="VIEW_3D"):
    a = area_of(area_type)
    r = region_of(a)
    win().cursor_warp(r.x + r.width // 2, r.y + r.height // 2)


# ---------------------------------------------------------------- シーン操作
def clear_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        with ctx():
            bpy.ops.object.mode_set(mode="OBJECT")
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    bpy.data.orphans_purge(do_recursive=True)


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


def select(objs, active=None):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or (objs[-1] if objs else None)


def mode(m):
    with ctx():
        bpy.ops.object.mode_set(mode=m)


def set_view(rot=(63.6, 0, 46.7), dist=12.0, loc=(0, 0, 0), persp="PERSP", area=None):
    area = area or area_of("VIEW_3D")
    r3d = area.spaces.active.region_3d
    r3d.view_perspective = persp
    r3d.view_rotation = Euler([radians(v) for v in rot]).to_quaternion()
    r3d.view_distance = dist
    r3d.view_location = Vector(loc)


def view_axis(axis, dist=None):
    with ctx():
        bpy.ops.view3d.view_axis(type=axis)
    if dist:
        area_of("VIEW_3D").spaces.active.region_3d.view_distance = dist


def props_tab(tab):
    try:
        area_of("PROPERTIES").spaces.active.context = tab
    except TypeError as e:
        log(f"props_tab({tab}) failed: {e}")


def expand_panel(cls_name):
    """既定で閉じているパネルを開いた状態で描画させる（そのタブを初めて表示する前に呼ぶ）"""
    cls = getattr(bpy.types, cls_name, None)
    if cls is None:
        log(f"expand_panel: {cls_name} not found")
        return
    options = set(getattr(cls, "bl_options", set())) - {"DEFAULT_CLOSED"}
    bpy.utils.unregister_class(cls)
    cls.bl_options = options
    bpy.utils.register_class(cls)


def collapse_panel(cls_name):
    """既定で開いているパネルを閉じた状態で描画させる"""
    cls = getattr(bpy.types, cls_name, None)
    if cls is None:
        return
    bpy.utils.unregister_class(cls)
    cls.bl_options = set(getattr(cls, "bl_options", set())) | {"DEFAULT_CLOSED"}
    bpy.utils.register_class(cls)


def maximize_foreground_window():
    """新しく開いたウィンドウ（ファイルブラウザ・プリファレンス）を最大化する"""
    hwnd = ctypes.windll.user32.GetForegroundWindow()
    ctypes.windll.user32.ShowWindow(hwnd, 3)


def sidebar(show=True, tab=None):
    a = area_of("VIEW_3D")
    a.spaces.active.show_region_ui = show
    if show and tab:
        for r in a.regions:
            if r.type == "UI":
                try:
                    r.active_panel_category = tab
                except Exception as e:   # このバージョンの Blender に、そのタブが無いときだけ。撮影は続けてログに残す
                    log(f"sidebar タブ {tab} を開けませんでした: {e}")


def overlay():
    return area_of("VIEW_3D").spaces.active.overlay


def shading():
    return area_of("VIEW_3D").spaces.active.shading


def material(name, color):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*color, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    m.diffuse_color = (*color, 1)  # ソリッド表示用
    return m


def add_cube(name="Cube", size=2, loc=(0, 0, 0)):
    with ctx():
        bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    o = bpy.context.object
    o.name = name
    return o


def default_scene():
    """起動直後と同じ立方体・ライト・カメラ"""
    cube = add_cube()
    cam = link(bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera")))
    cam.location = (7.36, -6.93, 4.96)
    cam.rotation_euler = (radians(63.6), 0, radians(46.7))
    light = link(bpy.data.objects.new("Light", bpy.data.lights.new("Light", "POINT")))
    light.location = (4.08, 1.0, 5.9)
    light.data.energy = 1000
    select([cube])
    return cube


def bm_of(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    return bm


def bm_to(bm, obj):
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def cube_seams(obj):
    """立方体を十字の展開図に開くシーム（7 本）"""
    bm = bm_of(obj)
    for e in bm.edges:
        a, b = e.verts[0].co, e.verts[1].co
        vertical = abs(a.z - b.z) > 1e-6
        if vertical:
            e.seam = a.x < 0 and a.y > 0          # 左奥の縦の辺だけ
        elif a.z > 0 and b.z > 0:
            e.seam = not (a.y < -0.9 and b.y < -0.9)   # 上面: 前以外の 3 辺
        else:
            e.seam = not (a.y < -0.9 and b.y < -0.9)   # 下面: 前以外の 3 辺
    bm_to(bm, obj)


def subdivide_vertical(obj, cuts=3):
    """立方体などの縦の辺を分割して横方向のループを増やす"""
    bm = bm_of(obj)
    edges = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-6
             and abs(e.verts[0].co.x - e.verts[1].co.x) < 1e-6 and abs(e.verts[0].co.y - e.verts[1].co.y) < 1e-6]
    bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts, use_grid_fill=True)
    bm_to(bm, obj)


# ---------------------------------------------------------------- 人型モデル（記事の例に使う簡単なアバター）
BONES = [
    # name, head, tail, parent, connect
    ("Hips", (0, 0, 0.95), (0, 0, 1.1), None, False),
    ("Spine", (0, 0, 1.1), (0, 0, 1.3), "Hips", True),
    ("Chest", (0, 0, 1.3), (0, 0, 1.5), "Spine", True),
    ("Neck", (0, 0, 1.5), (0, 0, 1.62), "Chest", True),
    ("Head", (0, 0, 1.62), (0, 0, 1.9), "Neck", True),
    ("Shoulder_L", (0.05, 0, 1.5), (0.17, 0, 1.5), "Chest", False),
    ("UpperArm_L", (0.17, 0, 1.5), (0.45, 0, 1.5), "Shoulder_L", True),
    ("LowerArm_L", (0.45, 0, 1.5), (0.72, 0, 1.5), "UpperArm_L", True),
    ("Hand_L", (0.72, 0, 1.5), (0.85, 0, 1.5), "LowerArm_L", True),
    ("UpperLeg_L", (0.1, 0, 0.95), (0.1, 0, 0.5), "Hips", False),
    ("LowerLeg_L", (0.1, 0, 0.5), (0.1, 0, 0.08), "UpperLeg_L", True),
    ("Foot_L", (0.1, 0, 0.08), (0.1, -0.15, 0.02), "LowerLeg_L", True),
]


def mirrored_bones():
    out = list(BONES)
    for name, h, t, parent, connect in BONES:
        if name.endswith("_L"):
            mir = lambda v: (-v[0], v[1], v[2])
            p = parent[:-2] + "_R" if parent and parent.endswith("_L") else parent
            out.append((name[:-2] + "_R", mir(h), mir(t), p, connect))
    return out


def build_body():
    bm = bmesh.new()

    def part(make, matrix):
        tmp = bmesh.new()
        make(tmp)
        bmesh.ops.transform(tmp, matrix=matrix, verts=tmp.verts)
        m = bpy.data.meshes.new("tmp")
        tmp.to_mesh(m)
        tmp.free()
        bm.from_mesh(m)
        bpy.data.meshes.remove(m)

    def cyl(r, depth, cuts, segs=16):
        def make(b):
            bmesh.ops.create_cone(b, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r, depth=depth)
            vedges = [e for e in b.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-6]
            bmesh.ops.subdivide_edges(b, edges=vedges, cuts=cuts, use_grid_fill=True)
        return make

    T = Matrix.Translation
    part(cyl(0.17, 0.7, 6, 24), T((0, 0, 1.25)))                       # 胴体
    part(cyl(0.05, 0.16, 1, 12), T((0, 0, 1.6)))                        # 首
    for sx in (1, -1):
        part(cyl(0.055, 0.68, 7), T((sx * 0.5, 0, 1.5)) @ Matrix.Rotation(radians(90), 4, "Y"))   # 腕
        part(cyl(0.08, 0.9, 7), T((sx * 0.1, 0, 0.5)))                                            # 脚

    def head(b):
        bmesh.ops.create_uvsphere(b, u_segments=16, v_segments=10, radius=0.14)
    part(head, T((0, 0, 1.8)))

    mesh = bpy.data.meshes.new("Body")
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    body = link(bpy.data.objects.new("Body", mesh))
    body.data.materials.append(material("Skin", (0.9, 0.72, 0.62)))
    return body


def build_armature(in_front=True):
    data = bpy.data.armatures.new("Armature")
    arm = link(bpy.data.objects.new("Armature", data))
    arm.show_in_front = in_front
    data.display_type = "OCTAHEDRAL"
    select([arm])
    mode("EDIT")
    eb = data.edit_bones
    for name, h, t, parent, connect in mirrored_bones():
        b = eb.new(name)
        b.head, b.tail = h, t
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
    mode("OBJECT")
    return arm


def bind(body, arm):
    """体をアーマチュアに親子付けし、ボーンまでの距離でウェイトを作る（自動ウェイトの代わり。結果が安定する）"""
    body.parent = arm
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    segs = []
    for name, h, t, *_ in mirrored_bones():
        segs.append((name, Vector(h), Vector(t)))
        body.vertex_groups.new(name=name)

    def dist(p, a, b):
        ab = b - a
        tt = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
        return (p - (a + ab * tt)).length

    for v in body.data.vertices:
        d = sorted(((dist(v.co, a, b), name) for name, a, b in segs))
        (d1, n1), (d2, n2) = d[0], d[1]
        if d2 < d1 * 1.6:
            w1 = d2 / (d1 + d2)
            body.vertex_groups[n1].add([v.index], w1, "REPLACE")
            body.vertex_groups[n2].add([v.index], 1 - w1, "REPLACE")
        else:
            body.vertex_groups[n1].add([v.index], 1.0, "REPLACE")


def build_mannequin():
    body = build_body()
    arm = build_armature()
    bind(body, arm)
    select([body])
    return body, arm


def build_shirt(body):
    """体の胴体と上腕の面を複製・分離してシャツにする（記事の手順と同じ）"""
    for p in body.data.polygons:
        c = p.center
        p.select = 1.0 <= c.z <= 1.58 and (abs(c.x) < 0.2 or (c.z > 1.42 and abs(c.x) < 0.5))
    select([body])
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_mode(type="FACE")
        bpy.ops.mesh.duplicate()
        bpy.ops.mesh.separate(type="SELECTED")
    mode("OBJECT")
    shirt = next(o for o in bpy.data.objects if o.name.startswith("Body.") and o is not body)
    shirt.name = "Shirt"
    select([shirt])
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.transform.shrink_fatten(value=0.012)
    mode("OBJECT")
    shirt.data.materials.clear()
    shirt.data.materials.append(material("Shirt", (0.25, 0.45, 0.85)))
    return shirt


def pose(arm, bone, euler_deg):
    select([arm])
    mode("POSE")
    pb = arm.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = [radians(v) for v in euler_deg]


# ---------------------------------------------------------------- ショットの登録と撮影
SHOTS = []


def shot(category, name, area="WINDOW", workspace="Layout", props=None, delay=0.6, maximize=None, menu=None, pie=False,
         window="MAIN", fullscreen=False):
    """撮影するものを登録する。

    area:      撮るエリア（'WINDOW' = ウィンドウ全体、'VIEW_3D' などはそのエリアだけ）
    workspace: 切り替えるワークスペース
    props:     プロパティエディターのタブ（'MODIFIER' など）
    maximize:  撮る前に最大化するエリアの種類（Ctrl+Space 相当）
    menu:      撮る直前に開くメニューの ID（pie=True でパイメニュー）
    window:    'MAIN' / 'PREFS' / 'FILE'（新しく開いたウィンドウを撮る）
    fullscreen: 新しいウィンドウを全画面にしてから撮る
    """
    def deco(fn):
        SHOTS.append(dict(category=category, name=name, area=area, workspace=workspace, props=props, delay=delay,
                          maximize=maximize, menu=menu, pie=pie, window=window, fullscreen=fullscreen, fn=fn))
        return fn
    return deco


def out_path(s):
    d = os.path.join(OUT, s["category"], "images")
    os.makedirs(d, exist_ok=True)
    return os.path.join(d, s["name"] + ".png")


def reset_ui():
    w = win()
    for extra in list(bpy.context.window_manager.windows)[1:]:
        with bpy.context.temp_override(window=extra, area=extra.screen.areas[0]):
            bpy.ops.wm.window_close()
    if w.workspace.name != "Layout":
        w.workspace = bpy.data.workspaces["Layout"]


def reset_view_settings():
    a = area_of("VIEW_3D")
    sp = a.spaces.active
    sp.show_region_ui = False
    sp.show_region_toolbar = True
    sp.shading.type = "SOLID"
    sp.overlay.show_stats = False
    sp.overlay.show_face_orientation = False
    sp.overlay.show_overlays = True
    set_view()
    with ctx():
        bpy.ops.wm.tool_set_by_id(name="builtin.select_box")
    area_of("PROPERTIES").spaces.active.search_filter = ""
    props_tab("OBJECT")


def capture(s, extra):
    """s['area'] に応じてスクリーンショットを撮り、後処理の情報を返す"""
    path = out_path(s)
    w = win()
    if s["window"] in ("PREFS", "FILE"):
        ws = bpy.context.window_manager.windows
        log(f"  windows={len(ws)} areas={[[a.type for a in x.screen.areas] for x in ws]}")
        w = ws[-1]
        a = w.screen.areas[0]
        with bpy.context.temp_override(window=w, area=a, region=region_of(a)):
            bpy.ops.screen.screenshot(filepath=path)
        return path
    if s["area"] == "WINDOW":
        with ctx(w=w):
            bpy.ops.screen.screenshot(filepath=path)
    else:
        a = area_of(s["area"], w)
        with bpy.context.temp_override(window=w, area=a, region=region_of(a)):
            bpy.ops.screen.screenshot_area(filepath=path)
    return path


def run_shot(s):
    """1 ショット分の手順。yield で待ち時間を返す"""
    log(f"shot {s['category']}/{s['name']}")
    clear_scene()
    reset_ui()
    yield 0.3
    reset_view_settings()
    if s["workspace"] != "Layout":
        win().workspace = bpy.data.workspaces[s["workspace"]]
        yield 0.5
    extra = s["fn"]() or {}
    if s["props"]:
        yield 0.3  # オブジェクトを作った直後はタブの一覧が更新されていない
        props_tab(s["props"])
    if s["maximize"]:
        a = area_of(s["maximize"])
        with bpy.context.temp_override(window=win(), area=a, region=region_of(a)):
            bpy.ops.screen.screen_full_area()
        yield 0.4
    if s["window"] in ("PREFS", "FILE") and s["fullscreen"]:
        yield 0.8
        maximize_foreground_window()
    if not s["menu"]:
        win().cursor_warp(700, 6)  # ステータスバーの上に置いてツールチップが出ないようにする
    yield s["delay"]
    if s["menu"]:
        area_type = extra.get("menu_area", "VIEW_3D")
        warp_to_center(area_type)
        yield 0.2
        with ctx(area_type):
            if s["pie"]:
                bpy.ops.wm.call_menu_pie(name=s["menu"])
            else:
                bpy.ops.wm.call_menu(name=s["menu"])
        yield 0.35
    path = capture(s, extra)
    crop = extra.get("crop")
    if extra.get("crop_fn"):
        tw = bpy.context.window_manager.windows[-1] if s["window"] in ("PREFS", "FILE") else win()
        crop = extra["crop_fn"](tw.width, tw.height)
    item = {"file": path, "crop": crop, "labels": extra.get("labels")}
    if item["crop"] or item["labels"]:
        POST.append(item)
    yield 0.2
    if s["menu"] or s["window"] == "FILE":
        press_esc()  # メニュー・ファイルブラウザを閉じる（ファイルブラウザは操作もキャンセルされる）
        yield 0.5
    if s["maximize"]:
        a = win().screen.areas[0]
        with bpy.context.temp_override(window=win(), area=a, region=region_of(a)):
            bpy.ops.screen.back_to_previous()
        yield 0.3
    reset_ui()
    yield 0.2


def main_gen():
    for s in SHOTS:
        if ONLY and s["name"] not in ONLY:
            continue
        try:
            yield from run_shot(s)
        except Exception:
            log(traceback.format_exc())
            press_esc()
            try:
                reset_ui()
            except Exception:
                log("reset_ui に失敗（次のショットに進む）:\n" + traceback.format_exc())
            yield 0.5
    with open(os.path.join(WORK, "post.json"), "w", encoding="utf-8") as f:
        json.dump(POST, f, ensure_ascii=False, indent=1)
    log("done")


# ---------------------------------------------------------------- 切り抜き用のヘルパー
def area_rect_in_window(a):
    """ウィンドウのスクリーンショット上でのエリアの矩形（左上原点）"""
    w = win()
    return (a.x, w.height - (a.y + a.height), a.x + a.width, w.height - a.y)


def region_rect_in_area(a, kind):
    r = region_of(a, kind)
    return (r.x - a.x, (a.y + a.height) - (r.y + r.height), r.x - a.x + r.width, (a.y + a.height) - r.y)


# ================================================================ getting-started
@shot("getting-started", "install-preferences-language", window="PREFS", fullscreen=True)
def _():
    with ctx():
        bpy.ops.screen.userpref_show()
    bpy.context.preferences.active_section = "INTERFACE"
    return {"crop_fn": lambda w, h: (0, 0, min(w, 1660), min(h, 640))}


@shot("getting-started", "install-extensions", window="PREFS", fullscreen=True, delay=1.5)
def _():
    with ctx():
        bpy.ops.screen.userpref_show()
    bpy.context.preferences.active_section = "EXTENSIONS"
    return {"crop_fn": lambda w, h: (0, 0, min(w, 1660), min(h, 900))}


@shot("getting-started", "interface-overview")
def _():
    default_scene()
    labels = []
    w = win()
    for n, kind in ((1, "VIEW_3D"), (2, "OUTLINER"), (3, "PROPERTIES"), (4, "DOPESHEET_EDITOR")):
        a = area_of(kind)
        x0, y0, x1, y1 = area_rect_in_window(a)
        labels.append({"n": n, "x": x0 + (140 if kind == "DOPESHEET_EDITOR" else 40), "y": y0 + 60 if kind == "VIEW_3D" else y0 + (y1 - y0) // 2})
    a = area_of("VIEW_3D")
    labels[0] = {"n": 1, "x": (a.x + a.width) // 2, "y": w.height - (a.y + a.height // 2) - 120}
    labels.append({"n": 5, "x": 700, "y": 16})
    x0, y0, x1, y1 = area_rect_in_window(a)
    labels.append({"n": 6, "x": x0 + 60, "y": y0 + 420})
    return {"labels": labels}


@shot("getting-started", "interface-sidebar", area="VIEW_3D")
def _():
    default_scene()
    sidebar(True, "Item")
    a = area_of("VIEW_3D")
    x0, y0, x1, y1 = region_rect_in_area(a, "UI")
    return {"crop": (x0 - 20, 0, x1, min(y1, 420))}


@shot("getting-started", "navigation-gizmo", area="VIEW_3D")
def _():
    default_scene()
    a = area_of("VIEW_3D")
    return {"crop": (a.width - 150, 40, a.width, 330)}


@shot("getting-started", "navigation-front-ortho", area="VIEW_3D")
def _():
    default_scene()
    view_axis("FRONT", dist=9)


# ================================================================ basics
@shot("basics", "select-objects", area="VIEW_3D")
def _():
    a = add_cube("Cube", loc=(-3, 0, 0))
    b = add_cube("Cube.001", loc=(0, 0, 0))
    c = add_cube("Cube.002", loc=(3, 0, 0))
    select([b, c], active=c)
    set_view(dist=11)


@shot("basics", "select-add-menu", menu="VIEW3D_MT_add")
def _():
    default_scene()


@shot("basics", "transform-move-gizmo", area="VIEW_3D")
def _():
    default_scene()
    with ctx():
        bpy.ops.wm.tool_set_by_id(name="builtin.move")
    set_view(dist=8)


@shot("basics", "transform-sidebar", area="VIEW_3D")
def _():
    cube = default_scene()
    cube.location = (1.5, 0, 0.5)
    cube.rotation_euler = (0, 0, radians(30))
    sidebar(True, "Item")
    a = area_of("VIEW_3D")
    x0, y0, x1, y1 = region_rect_in_area(a, "UI")
    return {"crop": (x0 - 20, 0, x1, min(y1, 420))}


@shot("basics", "transform-apply-menu", menu="VIEW3D_MT_object_apply")
def _():
    default_scene()


@shot("basics", "outliner-collections", area="OUTLINER")
def _():
    scene = bpy.context.scene
    cols = {}
    for name in ("Avatar", "Clothing", "Reference"):
        cols[name] = bpy.data.collections.new(name)
        scene.collection.children.link(cols[name])
    body, arm = build_mannequin()
    for o in (body, arm):
        scene.collection.objects.unlink(o)
        cols["Avatar"].objects.link(o)
    shirt = build_shirt(body)
    for c in shirt.users_collection:
        c.objects.unlink(shirt)
    cols["Clothing"].objects.link(shirt)
    ref = add_cube("Reference_Image", size=1, loc=(0, 2, 1))
    scene.collection.objects.unlink(ref)
    cols["Reference"].objects.link(ref)
    ref.hide_set(True)
    select([shirt])


@shot("basics", "origin-snap-pie", menu="VIEW3D_MT_snap_pie", pie=True)
def _():
    default_scene()


@shot("basics", "origin-header", area="VIEW_3D")
def _():
    default_scene()
    a = area_of("VIEW_3D")
    x0, y0, x1, y1 = region_rect_in_area(a, "HEADER")
    return {"crop": (300, y0, 1000, y1 + 28)}


@shot("basics", "modifiers-add-menu", menu="OBJECT_MT_modifier_add", props="MODIFIER")
def _():
    default_scene()
    return {"menu_area": "PROPERTIES"}


@shot("basics", "modifiers-panel", area="PROPERTIES", props="MODIFIER")
def _():
    cube = default_scene()
    m = cube.modifiers.new("Mirror", "MIRROR")
    m.use_clip = True
    s = cube.modifiers.new("Subdivision", "SUBSURF")
    s.levels = 2


# ================================================================ modeling
@shot("modeling", "edit-mode-select-modes", area="VIEW_3D")
def _():
    default_scene()
    mode("EDIT")
    a = area_of("VIEW_3D")
    x0, y0, x1, y1 = region_rect_in_area(a, "HEADER")
    return {"crop": (0, y0, 560, y1 + 2)}


@shot("modeling", "edit-mode-loop", area="VIEW_3D")
def _():
    cube = default_scene()
    subdivide_vertical(cube, cuts=3)
    bm = bm_of(cube)
    for e in bm.edges:
        e.select = False
    for v in bm.verts:
        v.select = False
    target = next(e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-6
                  and abs(e.verts[0].co.z - 0.0) < 1e-6 and e.verts[0].co.y < -0.9 and e.verts[1].co.y < -0.9)
    target.select = True
    for v in target.verts:
        v.select = True
    bm_to(bm, cube)
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_mode(type="EDGE")
        bpy.ops.mesh.loop_multi_select(ring=False)
    set_view(dist=7)


@shot("modeling", "extrude-face", area="VIEW_3D")
def _():
    cube = default_scene()
    for p in cube.data.polygons:
        p.select = p.normal.z > 0.9
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_mode(type="FACE")
        bpy.ops.mesh.extrude_region_move(TRANSFORM_OT_translate={"value": (0, 0, 1.2)})
    set_view(dist=8)


@shot("modeling", "inset-face", area="VIEW_3D")
def _():
    cube = default_scene()
    for p in cube.data.polygons:
        p.select = p.normal.z > 0.9
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_mode(type="FACE")
        bpy.ops.mesh.inset(thickness=0.35)
    set_view(dist=7)


@shot("modeling", "loopcut", area="VIEW_3D")
def _():
    cube = default_scene()
    mode("EDIT")
    bm = bmesh.from_edit_mesh(cube.data)
    bm.edges.ensure_lookup_table()
    idx = next(e.index for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-6)
    with ctx():
        bpy.ops.mesh.select_mode(type="EDGE")
        bpy.ops.mesh.loopcut_slide(MESH_OT_loopcut={"number_cuts": 3, "object_index": 0, "edge_index": idx})
    set_view(dist=7)


@shot("modeling", "bevel-edges", area="VIEW_3D")
def _():
    cube = default_scene()
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.select_mode(type="EDGE")
        bpy.ops.mesh.bevel(offset=0.25, segments=4, affect="EDGES")
    set_view(dist=7)


@shot("modeling", "merge-menu", menu="VIEW3D_MT_edit_mesh_merge")
def _():
    default_scene()
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")


@shot("modeling", "mirror-half", area="VIEW_3D")
def _():
    cube = default_scene()
    bm = bm_of(cube)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0), plane_no=(1, 0, 0))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().x < -1e-6], context="FACES")
    bm_to(bm, cube)
    m = cube.modifiers.new("Mirror", "MIRROR")
    m.use_clip = True
    m.show_in_editmode = True
    m.show_on_cage = False
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="DESELECT")
    set_view(dist=7)


@shot("modeling", "smooth-shading", area="VIEW_3D")
def _():
    with ctx():
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=(-1.4, 0, 0))
        flat = bpy.context.object
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=(1.4, 0, 0))
        smooth = bpy.context.object
    for p in smooth.data.polygons:
        p.use_smooth = True
    select([])
    set_view(rot=(80, 0, 0), dist=7)


@shot("modeling", "topology-stats", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    arm.hide_set(True)
    overlay().show_stats = True
    select([body])
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")
    set_view(rot=(80, 0, 20), dist=4.5, loc=(0, 0, 1))


@shot("modeling", "topology-face-orientation", area="VIEW_3D")
def _():
    cube = default_scene()
    subdivide_vertical(cube, cuts=1)
    bm = bm_of(cube)
    flip = [f for f in bm.faces if f.normal.y < -0.9 and f.calc_center_median().z > 0]
    bmesh.ops.reverse_faces(bm, faces=flip)
    bm_to(bm, cube)
    overlay().show_face_orientation = True
    set_view(rot=(70, 0, 20), dist=7)


# ================================================================ materials
@shot("materials", "material-properties", area="PROPERTIES", props="MATERIAL")
def _():
    cube = default_scene()
    cube.data.materials.append(material("Shirt", (0.25, 0.45, 0.85)))


@shot("materials", "material-shading-workspace", workspace="Shading", delay=1.5)
def _():
    cube = default_scene()
    cube.data.materials.append(material("Shirt", (0.25, 0.45, 0.85)))
    set_view(dist=6.5)


@shot("materials", "uv-editing-workspace", workspace="UV Editing", delay=1.2)
def _():
    cube = default_scene()
    cube_seams(cube)
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.unwrap(method="ANGLE_BASED", margin=0.02)
    set_view(dist=6.5)


@shot("materials", "uv-seams", area="VIEW_3D")
def _():
    cube = default_scene()
    cube_seams(cube)
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="DESELECT")
        bpy.ops.mesh.select_mode(type="EDGE")
    set_view(dist=7)


@shot("materials", "uv-unwrap-menu", menu="VIEW3D_MT_uv_map")
def _():
    default_scene()
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")


def image_material(name="Shirt"):
    m = material(name, (1, 1, 1))
    img = bpy.data.images.new("Shirt_Color", 1024, 1024)
    img.generated_type = "COLOR_GRID"
    nodes = m.node_tree.nodes
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.location = (-400, 300)
    nodes["Principled BSDF"].location = (0, 300)
    nodes["Material Output"].location = (320, 300)
    m.node_tree.links.new(tex.outputs["Color"], nodes["Principled BSDF"].inputs["Base Color"])
    return m


def uv_unwrapped_textured_cube():
    """画像のマテリアルを付けた立方体を、UV 展開して、シーンを組んだ状態で返す（UV の 2 本のショットで共有）。"""
    cube = default_scene()
    cube.data.materials.append(image_material())
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project()
    mode("OBJECT")
    set_view(dist=6.5)


@shot("materials", "texture-node", workspace="Shading", area="NODE_EDITOR", delay=1.5)
def _():
    uv_unwrapped_textured_cube()


@shot("materials", "texture-paint", workspace="Texture Paint", delay=1.5)
def _():
    uv_unwrapped_textured_cube()


@shot("materials", "bake-panel", area="PROPERTIES", props="RENDER", maximize="PROPERTIES", delay=0.8)
def _():
    default_scene()
    area_of("PROPERTIES").spaces.active.search_filter = "ベイク"
    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.render.bake.use_pass_direct = False
    bpy.context.scene.render.bake.use_pass_indirect = False
    bpy.context.scene.cycles.bake_type = "DIFFUSE"
    return {"crop": (0, 0, 1600, 700)}


# ================================================================ rigging
@shot("rigging", "armature-edit", area="VIEW_3D")
def _():
    arm = build_armature()
    select([arm])
    mode("EDIT")
    view_axis("FRONT", dist=3.2)
    area_of("VIEW_3D").spaces.active.region_3d.view_location = Vector((0, 0, 1.0))


@shot("rigging", "armature-properties", area="PROPERTIES", props="DATA")
def _():
    expand_panel("DATA_PT_display")
    arm = build_armature()
    select([arm])


@shot("rigging", "parent-menu", menu="VIEW3D_MT_object_parent")
def _():
    body = build_body()
    arm = build_armature()
    select([body, arm], active=arm)
    set_view(dist=5, loc=(0, 0, 1))


@shot("rigging", "parent-pose-test", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    pose(arm, "UpperArm_L", (0, 0, -60))
    pose(arm, "LowerArm_L", (0, 0, -40))
    set_view(rot=(75, 0, 25), dist=3.4, loc=(0.2, 0, 1.2))


@shot("rigging", "weight-paint-mode", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    select([body])
    body.vertex_groups.active_index = body.vertex_groups["UpperArm_L"].index
    mode("WEIGHT_PAINT")
    set_view(rot=(80, 0, 15), dist=3.4, loc=(0.2, 0, 1.2))


@shot("rigging", "weight-paint-vertex-groups", area="PROPERTIES", props="DATA")
def _():
    body, arm = build_mannequin()
    select([body])
    body.vertex_groups.active_index = body.vertex_groups["UpperArm_L"].index


@shot("rigging", "shape-keys-panel", area="PROPERTIES", props="DATA")
def _():
    cube = default_scene()
    cube.shape_key_add(name="Basis")
    k = cube.shape_key_add(name="Big", from_mix=False)
    for d in k.data:
        d.co = d.co * 1.3
    k2 = cube.shape_key_add(name="Slim", from_mix=False)
    for d in k2.data:
        d.co = Vector((d.co.x * 0.8, d.co.y * 0.8, d.co.z))
    k.value = 0.5
    cube.active_shape_key_index = 1


# ================================================================ avatar-clothing
@shot("avatar-clothing", "import-result", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    select([arm])
    set_view(rot=(78, 0, 25), dist=4.2, loc=(0, 0, 1.0))


@shot("avatar-clothing", "clothing-duplicate", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    arm.hide_set(True)
    shirt = build_shirt(body)
    select([shirt])
    set_view(rot=(78, 0, 25), dist=2.6, loc=(0, 0, 1.3))


@shot("avatar-clothing", "clothing-solidify", area="VIEW_3D", props="MODIFIER")
def _():
    body, arm = build_mannequin()
    arm.hide_set(True)
    shirt = build_shirt(body)
    m = shirt.modifiers.new("Solidify", "SOLIDIFY")
    m.thickness = 0.015
    m.offset = 1
    select([shirt])
    set_view(rot=(70, 0, 35), dist=1.6, loc=(0.1, 0, 1.15))


def shirt_seams(shirt):
    bm = bm_of(shirt)
    for e in bm.edges:
        a, b = e.verts[0].co, e.verts[1].co
        side = abs(a.x) > 0.16 and abs(b.x) > 0.16 and abs(a.z - b.z) > 1e-6 and abs(a.y) < 0.03 and abs(b.y) < 0.03
        sleeve_ring = abs(abs(a.x) - 0.2) < 0.03 and abs(abs(b.x) - 0.2) < 0.03 and abs(a.x - b.x) < 1e-4 and a.z > 1.4
        under_arm = a.z < 1.46 and b.z < 1.46 and a.z > 1.4 and abs(a.x) > 0.2 and abs(b.x) > 0.2 and abs(a.z - b.z) < 1e-4
        e.seam = side or sleeve_ring or under_arm
    bm_to(bm, shirt)


@shot("avatar-clothing", "clothing-uv-seams", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    shirt = build_shirt(body)
    shirt_seams(shirt)
    arm.hide_set(True)
    body.hide_set(True)
    select([shirt])
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="DESELECT")
        bpy.ops.mesh.select_mode(type="EDGE")
    set_view(rot=(80, 0, 30), dist=2.2, loc=(0, 0, 1.3))


@shot("avatar-clothing", "clothing-uv-unwrap", workspace="UV Editing", delay=1.2)
def _():
    body, arm = build_mannequin()
    shirt = build_shirt(body)
    shirt_seams(shirt)
    arm.hide_set(True)
    body.hide_set(True)
    select([shirt])
    mode("EDIT")
    with ctx():
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.unwrap(method="ANGLE_BASED", margin=0.02)
    a = area_of("VIEW_3D")
    set_view(rot=(80, 0, 30), dist=2.4, loc=(0, 0, 1.3), area=a)


@shot("avatar-clothing", "clothing-data-transfer", area="PROPERTIES", props="MODIFIER")
def _():
    body, arm = build_mannequin()
    shirt = build_shirt(body)
    for vg in list(shirt.vertex_groups):
        shirt.vertex_groups.remove(vg)
    dt = shirt.modifiers.new("DataTransfer", "DATA_TRANSFER")
    dt.object = body
    dt.use_vert_data = True
    dt.data_types_verts = {"VGROUP_WEIGHTS"}
    dt.vert_mapping = "POLYINTERP_NEAREST"
    dt.show_expanded = True
    shirt.modifiers.move(len(shirt.modifiers) - 1, 0)
    shirt.modifiers["Armature"].show_expanded = False
    select([shirt])


@shot("avatar-clothing", "clothing-pose-check", area="VIEW_3D")
def _():
    body, arm = build_mannequin()
    shirt = build_shirt(body)
    m = shirt.modifiers.new("Solidify", "SOLIDIFY")
    m.thickness = 0.012
    m.offset = 1
    pose(arm, "UpperArm_L", (0, 0, -70))
    pose(arm, "UpperArm_R", (0, 0, 40))
    arm.show_in_front = False
    set_view(rot=(78, 0, 20), dist=3.0, loc=(0, 0, 1.25))


@shot("avatar-clothing", "export-fbx-dialog", window="FILE", fullscreen=True, delay=1.0)
def _():
    body, arm = build_mannequin()
    shirt = build_shirt(body)
    select([shirt, arm], active=arm)
    with ctx():
        bpy.ops.export_scene.fbx("INVOKE_DEFAULT", filepath=os.path.join(WORK, "Shirt.fbx"), use_selection=True,
                                 object_types={"ARMATURE", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                                 add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
    return {"crop_fn": lambda w, h: (w - 290, 40, w, min(h, 680))}


@shot("avatar-clothing", "import-fbx-dialog", window="FILE", fullscreen=True, delay=1.0)
def _():
    with ctx():
        bpy.ops.import_scene.fbx("INVOKE_DEFAULT")
    return {"crop_fn": lambda w, h: (w - 290, 40, w, min(h, 720))}



# ---------------------------------------------------------------- 実行
_gen = main_gen()


def _tick():
    try:
        return next(_gen)
    except StopIteration:
        bpy.ops.wm.quit_blender()
        return None
    except Exception:
        log(traceback.format_exc())
        bpy.ops.wm.quit_blender()
        return None


def _watchdog():
    log("watchdog: timeout, quitting")
    bpy.ops.wm.quit_blender()
    return None


open(LOG, "w").close()
bpy.app.timers.register(_tick, first_interval=2.0)
bpy.app.timers.register(_watchdog, first_interval=600.0)
