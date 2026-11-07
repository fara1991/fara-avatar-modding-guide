using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 記事用のスクリーンショットを撮る。Unity 自身の描画（GUIView.GrabPixels）を読むので、画面の上に重なる他のアプリは写らない。
[InitializeOnLoad]
public static class ShotRunner
{
    static readonly string Root = Path.GetFullPath(".");
    static readonly string OutDir = Path.Combine(Root, "ShotsOut");
    static int step;
    static double nextAt;
    static GameObject avatar, poseLib, merged, aao;

    static ShotRunner()
    {
        if (!File.Exists(Path.Combine(Root, "go.flag")) || File.Exists(Path.Combine(Root, "done.flag"))) return;
        Directory.CreateDirectory(OutDir);
        nextAt = EditorApplication.timeSinceStartup + 8;
        EditorApplication.update += Tick;
        Log("start");
    }

    static void Log(string s) => File.AppendAllText(Path.Combine(OutDir, "status.txt"), DateTime.Now.ToString("HH:mm:ss ") + s + "\n");

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).FirstOrDefault(t => t.Name == name);

    static void Tick()
    {
        if (waitingTag != null)
        {
            if (!File.Exists(Path.Combine(Root, "ack-" + waitingTag + ".flag")) && EditorApplication.timeSinceStartup < waitUntil) return;
            Log("acked " + waitingTag); waitingTag = null;
        }
        if (EditorApplication.timeSinceStartup < nextAt || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            switch (step)
            {
                case 0: Setup(); step = 1; nextAt = EditorApplication.timeSinceStartup + 3; break;
                case 1: ShowConsole(); SelectAndShow(avatar, collapseAll: false); step = 2; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 2: Capture("overview"); step = 3; nextAt = EditorApplication.timeSinceStartup + 1; break;
                case 3: SelectAndShow(aao, collapseAll: false); step = 4; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 4: ExpandAao(); step = 41; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 41: Capture("aao-trace"); step = 5; nextAt = EditorApplication.timeSinceStartup + 1; break;
                case 5: SelectAndShow(merged, collapseAll: false); step = 51; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 51: CollapseForMerge(); step = 6; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 6: Capture("aao-merge"); step = 7; nextAt = EditorApplication.timeSinceStartup + 1; break;
                case 7: SelectAndShow(poseLib, collapseAll: false); step = 8; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 8: Capture("apl"); step = 81; nextAt = EditorApplication.timeSinceStartup + 1; break;
                case 81: ExpandApl(); step = 82; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 82: Capture("apl-open"); step = 9; nextAt = EditorApplication.timeSinceStartup + 1; break;
                default:
                    File.WriteAllText(Path.Combine(Root, "done.flag"), "ok");
                    Log("done");
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception e)
        {
            Log("ERROR step " + step + ": " + e);
            File.WriteAllText(Path.Combine(Root, "done.flag"), "error");
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }

    static void Setup()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        avatar = new GameObject("Avatar");
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body"; body.transform.SetParent(avatar.transform, false); body.transform.localPosition = new Vector3(0, 1, 0);
        var desc = FindType("VRCAvatarDescriptor");
        if (desc != null) avatar.AddComponent(desc);

        var tao = FindType("TraceAndOptimize");
        aao = new GameObject("Avatar (AAO)");
        if (tao != null) aao.AddComponent(tao); else Log("TraceAndOptimize type not found");

        merged = new GameObject("Merged");
        merged.AddComponent<SkinnedMeshRenderer>();
        var msm = FindType("MergeSkinnedMesh");
        if (msm != null) merged.AddComponent(msm); else Log("MergeSkinnedMesh type not found");

        poseLib = new GameObject("Pose Library");
        poseLib.transform.SetParent(avatar.transform, false);
        var apl = FindType("AvatarPoseLibrary");
        if (apl != null)
        {
            var c = poseLib.AddComponent(apl);
            var dataF = apl.GetField("data");
            var data = dataF.GetValue(c);
            if (data == null) { data = Activator.CreateInstance(dataF.FieldType); dataF.SetValue(c, data); }
            apl.GetField("isInitialized").SetValue(c, true);
            var dt = data.GetType();
            dt.GetField("name").SetValue(data, "My Poses");
            var catT = FindType("PoseCategory"); var poseT = FindType("PoseEntry");
            var cats = (System.Collections.IList)dt.GetField("categories").GetValue(data);
            cats.Clear();
            var cat = Activator.CreateInstance(catT);
            catT.GetField("name").SetValue(cat, "Sit");
            var poses = (System.Collections.IList)catT.GetField("poses").GetValue(cat);
            foreach (var pn in new[] { "Sit Cross-legged", "Sit Hug Knees" })
            {
                var p = Activator.CreateInstance(poseT);
                poseT.GetField("name").SetValue(p, pn);
                var clip = new AnimationClip { name = pn };
                AssetDatabase.CreateAsset(clip, "Assets/" + pn.Replace(' ', '_') + ".anim");
                poseT.GetField("animationClip").SetValue(p, clip);
                poses.Add(p);
            }
            cats.Add(cat);
            EditorUtility.SetDirty(c);
        }
        else Log("AvatarPoseLibrary type not found");

        EditorSceneManager.SaveScene(scene, "Assets/Demo.unity");
        AssetDatabase.SaveAssets();
        Log("setup ok");
    }

    static void SelectAndShow(GameObject go, bool collapseAll)
    {
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        var inspector = FindType("InspectorWindow");
        if (inspector != null) EditorWindow.GetWindow(inspector).Show();
        foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>()) w.Repaint();
        InternalEditorRepaint();
        Log("selected " + go.name);
    }

    static void InternalEditorRepaint() => UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

    static void ShowConsole()
    {
        var cw = FindType("ConsoleWindow");
        if (cw != null) EditorWindow.GetWindow(cw).Show();
        var le = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
        le?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        Log("console shown");
    }

    static void CollapseForMerge()
    {
        foreach (var c in merged.GetComponents<Component>())
        {
            if (c == null) continue;
            var n = c.GetType().Name;
            if (n == "Transform" || n == "SkinnedMeshRenderer") UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(c, false);
            if (n == "MergeSkinnedMesh")
            {
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                while (it.Next(true)) { if (it.hasVisibleChildren) it.isExpanded = true; }
                so.ApplyModifiedProperties();
            }
        }
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        Log("collapsed for merge");
    }

    static void ExpandAao()
    {
        var ed = ActiveEditorTracker.sharedTracker.activeEditors.FirstOrDefault(e => e.GetType().Name == "TraceAndOptimizeEditor");
        if (ed == null) { Log("ExpandAao: editor missing"); return; }
        var fld = ed.GetType().GetField("_openFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fld == null) { Log("ExpandAao: _openFeatures missing"); return; }
        fld.SetValue(ed, true);
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        Log("ExpandAao: Features opened");
    }

    static void ExpandApl()
    {
        var c = poseLib.GetComponents<Component>().FirstOrDefault(x => x != null && x.GetType().Name == "AvatarPoseLibrary");
        if (c == null) { Log("ExpandApl: component missing"); return; }
        var tracker = ActiveEditorTracker.sharedTracker;
        var ed = tracker.activeEditors.FirstOrDefault(e => e.GetType().Name == "AvatarPoseLibraryEditor");
        if (ed == null) { Log("ExpandApl: editor missing"); return; }
        var et = ed.GetType();
        var dictF = et.GetField("_foldout", BindingFlags.Instance | BindingFlags.NonPublic);
        var dict = (System.Collections.IDictionary)dictF.GetValue(ed);
        var dataObj = c.GetType().GetField("data").GetValue(c);
        var cats = (System.Collections.IList)dataObj.GetType().GetField("categories").GetValue(dataObj);
        var getParam = et.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Where(m => m.Name == "GetParameter").ToArray();
        int n = 0;
        foreach (var cat in cats)
        {
            var poses = (System.Collections.IList)cat.GetType().GetField("poses").GetValue(cat);
            foreach (var p in poses)
            {
                var m = getParam.FirstOrDefault(x => x.GetParameters()[0].ParameterType == p.GetType());
                if (m == null) continue;
                var key = (string)m.Invoke(ed, new object[] { p });
                dict[key] = true; n++;
            }
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        Log("ExpandApl: opened " + n);
    }

    static string waitingTag;
    static double waitUntil;

    static void Capture(string tag)
    {
        File.WriteAllText(Path.Combine(Root, "ready-" + tag + ".flag"), "ready");
        waitingTag = tag;
        waitUntil = EditorApplication.timeSinceStartup + 60;
        Log("ready " + tag);
    }
}
