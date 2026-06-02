#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Configura el Nivel 1 (Quipto). Reentrante + Undo.
// Menu: Tools > Melao > Setup Nivel 1 (Quipto)
//
//   Ponky, Melo-Loka, dulceFrutas, Lengua, Oblea, Cuquis, GetaPata -> Ground
//   Wafer, Drulas, Pluck, Bocacdillo, Gelatina Colores             -> Wall
//   QuesoPUNTEAGUDO, Espadita                                       -> pinchos (kill)
//   Arequipe (+plane)                                              -> rio mortal (Changua + olas)
//   GelatinaProhibida                                             -> plataforma saltarina (bounce)
//   Player                                                        -> PlayerRespawn (controles normales)
//   Main Camera                                                   -> CameraStraightener + nivelar
public static class QuiptoSetupTool
{
    private const int LAYER_GROUND = 8, LAYER_WALL = 9, LAYER_PLATFORM = 10,
                      LAYER_CHANGUA = 11, LAYER_DEFAULT = 0, LAYER_PLAYER = 7;

    [MenuItem("Tools/Melao/Setup Nivel 1 (Quipto)")]
    public static void RunSetup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[Quipto] No hay escena activa."); return; }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Nivel 1 Quipto");

        var all = new List<GameObject>();
        foreach (var r in scene.GetRootGameObjects()) Collect(r.transform, all);
        var stats = new Dictionary<string, int>();

        foreach (var go in all)
        {
            if (go == null) continue;
            string n = go.name.ToLowerInvariant();

            if (go.GetComponent<PlayerController2_5D>() != null)
            { SetupPlayer(go); Bump(stats, "Player"); continue; }

            if (n.StartsWith("quesopunteagudo") || n.StartsWith("espadita"))
            { SetupSpike(go); Bump(stats, "Pinchos"); continue; }

            if (n.StartsWith("arequipe"))
            { SetupRiver(go); Bump(stats, "Arequipe (rio)"); continue; }

            if (n.StartsWith("gelatinaprohibida"))
            { SetupBounce(go); Bump(stats, "GelatinaProhibida (bounce)"); continue; }

            if (n.StartsWith("wafer") || n.StartsWith("drulas") || n.StartsWith("pluck") ||
                n.StartsWith("bocacdillo") || n.StartsWith("gelatina"))
            { SetGround(go, LAYER_WALL); Bump(stats, "Wall"); continue; }

            if (n.StartsWith("ponky") || n.StartsWith("melo-loka") || n.StartsWith("dulcefrutas") ||
                n.StartsWith("lengua") || n.StartsWith("oblea") || n.StartsWith("cuquis") ||
                n.StartsWith("getapata"))
            { SetGround(go, LAYER_GROUND); Bump(stats, "Ground"); continue; }
        }

        SetupCamera();
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undo);

        string summary = string.Join("\n", stats.OrderBy(k => k.Key).Select(k => $"  {k.Key}: {k.Value}"));
        Debug.Log($"[Quipto] Listo.\n{summary}");
        EditorUtility.DisplayDialog("Setup Nivel 1 Quipto", $"{summary}\n\nGuarda con Ctrl+S.", "OK");
    }

    private static void SetupPlayer(GameObject go)
    {
        if (go.GetComponent<PlayerRespawn>() == null) Undo.AddComponent<PlayerRespawn>(go);
        var pc = go.GetComponent<PlayerController2_5D>();
        if (pc != null) { Undo.RecordObject(pc, "Player"); pc.invertHorizontalInput = true; EditorUtility.SetDirty(pc); }
    }

    private static void SetupSpike(GameObject go)
    {
        SetLayer(go, LAYER_DEFAULT);
        Colliders(go, true, true);
        foreach (var h in Hosts<HazardKill>(go, false)) Hazard(h);
    }

    private static void SetGround(GameObject go, int layer)
    {
        SetLayer(go, layer);
        Colliders(go, false, false);
    }

    private static void SetupBounce(GameObject go)
    {
        SetLayer(go, LAYER_PLATFORM);
        Colliders(go, true, false);
        Hosts<BouncyPlatform>(go, true);
    }

    private static void SetupRiver(GameObject go)
    {
        SetLayer(go, LAYER_CHANGUA);
        AddLiquidTrigger(go);
        var h = go.GetComponent<HazardKill>(); if (h == null) h = Undo.AddComponent<HazardKill>(go);
        Hazard(h);
        // Olas en cada malla + Read/Write del mesh fuente.
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.GetComponent<ArequipeMovimiento>() == null) Undo.AddComponent<ArequipeMovimiento>(mf.gameObject);
            EnableReadWrite(mf.sharedMesh);
        }
    }

    private static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) { var t = GameObject.FindWithTag("MainCamera"); if (t != null) cam = t.GetComponent<Camera>(); }
        if (cam == null) return;
        if (cam.GetComponent<CameraStraightener>() == null) Undo.AddComponent<CameraStraightener>(cam.gameObject);
        Undo.RecordObject(cam.transform, "Level Camera");
        Vector3 e = cam.transform.eulerAngles; e.z = 0f; cam.transform.eulerAngles = e;
    }

    // ---- helpers ----
    private static void EnableReadWrite(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path)) return;
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }
    }

    private static void AddLiquidTrigger(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        var box = go.GetComponent<BoxCollider>(); if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = true;
        if (rends.Length == 0) return;
        Bounds wb = rends[0].bounds; for (int i = 1; i < rends.Length; i++) wb.Encapsulate(rends[i].bounds);
        const float depth = 3f;
        Vector3 wc = new Vector3(wb.center.x, wb.max.y - depth * 0.5f, wb.center.z);
        Vector3 lc = go.transform.InverseTransformPoint(wc);
        Vector3 s = go.transform.lossyScale;
        box.center = lc;
        box.size = new Vector3(Mathf.Max(wb.size.x, 0.5f) / Mathf.Max(1e-4f, Mathf.Abs(s.x)),
                               depth / Mathf.Max(1e-4f, Mathf.Abs(s.y)),
                               Mathf.Max(wb.size.z, 0.5f) / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
    }

    private static void Hazard(HazardKill h)
    { Undo.RecordObject(h, "Hazard"); h.onlyKillFromTop = false; h.playerLayer = 1 << LAYER_PLAYER; EditorUtility.SetDirty(h); }

    private static void Collect(Transform t, List<GameObject> bag)
    { bag.Add(t.gameObject); for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), bag); }

    private static void SetLayer(GameObject go, int layer)
    { Undo.RecordObject(go, "Layer"); go.layer = layer; foreach (Transform c in go.transform) SetLayer(c.gameObject, layer); }

    private static void Colliders(GameObject root, bool convex, bool trigger)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0)
        { if (root.GetComponent<Collider>() == null) { var b = Undo.AddComponent<BoxCollider>(root); b.isTrigger = trigger; } return; }
        foreach (var mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            var go = mf.gameObject;
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) { if (go.GetComponent<Collider>() != null && go == root) continue; mc = Undo.AddComponent<MeshCollider>(go); }
            Undo.RecordObject(mc, "MeshCollider");
            mc.sharedMesh = mf.sharedMesh; mc.convex = convex; mc.isTrigger = trigger; EditorUtility.SetDirty(mc);
        }
    }

    private static List<T> Hosts<T>(GameObject root, bool solidOnly) where T : Component
    {
        var res = new List<T>(); var seen = new HashSet<GameObject>();
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c == null || (solidOnly && c.isTrigger)) continue;
            if (!seen.Add(c.gameObject)) continue;
            var comp = c.gameObject.GetComponent<T>(); if (comp == null) comp = Undo.AddComponent<T>(c.gameObject);
            res.Add(comp);
        }
        return res;
    }

    private static void Bump(Dictionary<string, int> d, string k) { if (!d.ContainsKey(k)) d[k] = 0; d[k]++; }
}
#endif
