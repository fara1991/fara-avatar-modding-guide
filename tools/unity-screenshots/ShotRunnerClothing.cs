using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 記事「アバター用衣装の Unity での設定」用のスクリーンショットを撮る（Inspector の表示）。
[InitializeOnLoad]
public static class ShotRunnerClothing
{
    static readonly string Root = Path.GetFullPath(".");
    static readonly string OutDir = Path.Combine(Root, "ShotsOut");
    const string FbxPath = "Assets/Clothing/Shirt/Shirt.fbx";
    static int step;
    static double nextAt;
    static GameObject shirt, shirtMesh;

    static ShotRunnerClothing()
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

    static string waitingTag;
    static double waitUntil;

    static void Capture(string tag)
    {
        File.WriteAllText(Path.Combine(Root, "ready-" + tag + ".flag"), "ready");
        waitingTag = tag;
        waitUntil = EditorApplication.timeSinceStartup + 60;
        Log("ready " + tag);
    }

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
                case 1: SelectFbx(); step = 2; nextAt = EditorApplication.timeSinceStartup + 3; break;
                case 2: SetRigTab(); step = 3; nextAt = EditorApplication.timeSinceStartup + 2; break;
                case 3: Capture("model-rig"); step = 4; nextAt = EditorApplication.timeSinceStartup + 1; break;
                case 4: Show(shirt); Collapse(shirt, "Transform"); step = 5; nextAt = EditorApplication.timeSinceStartup + 6; break;
                case 5: Capture("merge-armature"); step = 6; nextAt = EditorApplication.timeSinceStartup + 1; break;
                case 6: Show(shirtMesh); Collapse(shirtMesh, "Transform"); Collapse(shirtMesh, "SkinnedMeshRenderer"); step = 7; nextAt = EditorApplication.timeSinceStartup + 6; break;
                case 7: Capture("blendshape-sync"); step = 8; nextAt = EditorApplication.timeSinceStartup + 1; break;
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
        var avatar = new GameObject("Avatar");
        var descT = FindType("VRCAvatarDescriptor");
        if (descT != null) avatar.AddComponent(descT); else Log("VRCAvatarDescriptor not found");
        shirt = new GameObject("Shirt");
        shirt.transform.SetParent(avatar.transform, false);
        var mergeT = FindType("ModularAvatarMergeArmature");
        if (mergeT != null) shirt.AddComponent(mergeT); else Log("ModularAvatarMergeArmature not found");
        var meshT = FindType("ModularAvatarMeshSettings");
        if (meshT != null) shirt.AddComponent(meshT); else Log("ModularAvatarMeshSettings not found");

        shirtMesh = new GameObject("Shirt_Mesh");
        shirtMesh.transform.SetParent(shirt.transform, false);
        shirtMesh.AddComponent<SkinnedMeshRenderer>();
        var syncT = FindType("ModularAvatarBlendshapeSync");
        if (syncT != null) shirtMesh.AddComponent(syncT); else Log("ModularAvatarBlendshapeSync not found");

        EditorSceneManager.SaveScene(scene, "Assets/Demo.unity");
        AssetDatabase.SaveAssets();
        Log("setup ok; fbx exists: " + (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) != null));
    }

    static void ShowInspector()
    {
        var inspector = FindType("InspectorWindow");
        if (inspector != null) EditorWindow.GetWindow(inspector).Show();
    }

    static void Show(GameObject go)
    {
        Selection.activeGameObject = go;
        ShowInspector();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        Log("selected " + go.name);
    }

    static void SelectFbx()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbx == null) throw new Exception("FBX not found: " + FbxPath);
        Selection.activeObject = fbx;
        EditorGUIUtility.PingObject(fbx);
        ShowInspector();
        Log("selected fbx");
    }

    static void SetRigTab()
    {
        var tracker = ActiveEditorTracker.sharedTracker;
        var ed = tracker.activeEditors.FirstOrDefault(e => e.GetType().Name == "ModelImporterEditor");
        if (ed == null) { Log("ModelImporterEditor not active: " + string.Join(",", tracker.activeEditors.Select(e => e.GetType().Name))); return; }
        bool done = false;
        for (var t = ed.GetType(); t != null; t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (f.FieldType == typeof(int)) Log("int field " + t.Name + "." + f.Name + " = " + f.GetValue(ed));
                if (f.FieldType == typeof(int) && f.Name == "m_ActiveEditorIndex") { f.SetValue(ed, 1); done = true; }
            }
        }
        var prop = ed.GetType().BaseType.GetProperty("activeTab", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Log("activeTab property: " + (prop != null) + "; index set: " + done);
        var tabsF = ed.GetType().BaseType.GetField("m_Tabs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (tabsF != null && prop != null && prop.CanWrite)
        {
            var tabs = (Array)tabsF.GetValue(ed);
            prop.SetValue(ed, tabs.GetValue(1));
            Log("activeTab set to tab 1 of " + tabs.Length);
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    static void Collapse(GameObject go, string typeName)
    {
        foreach (var c in go.GetComponents<Component>())
            if (c != null && c.GetType().Name == typeName) UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(c, false);
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
