# 撮影用の一時プリファレンスを作る。capture.ps1 から blender -b --factory-startup --python mkprefs.py で実行される。
# BLENDER_USER_CONFIG を一時フォルダに向けているので、普段使っている設定には影響しない。
import bpy

p = bpy.context.preferences
p.view.language = "ja_JP"
p.view.use_translate_interface = True
p.view.use_translate_tooltips = True
p.view.use_translate_new_dataname = False  # 記事の方針と同じ（新規データは英語名）
p.view.show_splash = False
p.view.show_developer_ui = False
p.view.ui_scale = 1.0
p.filepaths.use_auto_save_temporary_files = False
p.use_preferences_save = False
bpy.ops.wm.save_userpref()
