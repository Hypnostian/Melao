#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Herramienta de setup del sistema de power-ups (consistente con las demas de
// Tools > Melao). Hace dos cosas, reentrante (no duplica):
//
//   1) Crea un PREFAB de recoleccion por cada modelo FBX en Code/powerups:
//        Cuquis.fbx   -> Cuquis    (disparar cuquis)
//        HEARTBIG.fbx -> Heart     (recuperar vida)
//        GCHIPBIG.fbx -> Chips     (volverse pequena)
//        Merengue.fbx -> Merengue  (volverse grande)
//      Cada prefab: modelo + PowerUpPickup (con su tipo) + BoxCollider TRIGGER
//      ajustado a la malla. Quedan girando/flotando y al tocarlos Pops los guarda.
//
//   2) Asegura que el Player prefab tenga PowerUpController + PlayerSizeModifier.
//
// Menu: Tools > Melao > Setup PowerUps
public static class PowerUpSetupTool
{
    private const string PU_DIR     = "Assets/Code/powerups";
    private const string PREFAB_DIR = "Assets/Code/prefabs/PowerUps";
    private const string PLAYER_PREFAB = "Assets/Code/prefabs/Player.prefab";

    private struct Def
    {
        public string fbx;          // nombre del modelo en Code/powerups (sin .fbx)
        public PowerUpType type;
        public string shortName;    // nombre del prefab
    }

    private static Def[] Defs() => new[]
    {
        new Def { fbx = "Cuquis",   type = PowerUpType.Cuquis,   shortName = "Cuquis"   },
        new Def { fbx = "HEARTBIG", type = PowerUpType.Heart,    shortName = "Heart"    },
        new Def { fbx = "GCHIPBIG", type = PowerUpType.Chips,    shortName = "Chips"    },
        new Def { fbx = "Merengue", type = PowerUpType.Merengue, shortName = "Merengue" },
    };

    [MenuItem("Tools/Melao/Setup PowerUps")]
    public static void Run()
    {
        EnsureFolder();

        int made = 0;
        var report = new System.Text.StringBuilder();
        foreach (var d in Defs())
        {
            if (CreatePickupPrefab(d)) { made++; report.Append("  ").Append(d.shortName).Append(" -> ").Append(d.type).Append('\n'); }
            else report.Append("  (falta FBX) ").Append(d.fbx).Append('\n');
        }

        // Proyectil de la cuqui hecho con el MISMO FBX (no esfera/aceite).
        GameObject cuquiProj = CreateCuquiProjectilePrefab();

        bool playerOk = EnsurePlayerComponents(cuquiProj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"Prefabs de power-up creados en {PREFAB_DIR}:\n\n{report}\n" +
                     (cuquiProj != null ? "Proyectil cuqui (FBX) creado y asignado.\n"
                                        : "AVISO: no se pudo crear el proyectil cuqui.\n") +
                     (playerOk ? "Player: PowerUpController + PlayerSizeModifier configurados.\n"
                               : "AVISO: no se encontro el Player prefab; añade PowerUpController manualmente.\n") +
                     "\nArrastra los prefabs al mapa donde quieras los power-ups.";
        Debug.Log("[PowerUpSetup] Listo.\n" + msg);
        EditorUtility.DisplayDialog("Setup PowerUps", msg, "OK");
    }

    // ---------------------------------------------------------------
    //   PREFAB DE RECOLECCION
    // ---------------------------------------------------------------
    private static bool CreatePickupPrefab(Def d)
    {
        string fbxPath = $"{PU_DIR}/{d.fbx}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) { Debug.LogWarning($"[PowerUpSetup] No se encontro {fbxPath}"); return false; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (inst == null) return false;
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;

        // Collider TRIGGER ajustado a la malla.
        FitBoxTrigger(inst);

        // Componente de recoleccion con su tipo.
        var pickup = inst.GetComponent<PowerUpPickup>();
        if (pickup == null) pickup = inst.AddComponent<PowerUpPickup>();
        pickup.type = d.type;

        string prefabPath = $"{PREFAB_DIR}/{d.shortName}_PowerUp.prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        return true;
    }

    // Ajusta (o crea) un BoxCollider trigger al volumen combinado de los renderers,
    // en espacio local y respetando la escala del transform.
    private static void FitBoxTrigger(GameObject go)
    {
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
            return;
        }

        Bounds wb = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) wb.Encapsulate(renderers[i].bounds);

        Vector3 s = go.transform.lossyScale;
        float sx = Mathf.Abs(s.x) < 1e-4f ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Abs(s.y) < 1e-4f ? 1f : Mathf.Abs(s.y);
        float sz = Mathf.Abs(s.z) < 1e-4f ? 1f : Mathf.Abs(s.z);

        box.center = go.transform.InverseTransformPoint(wb.center);
        box.size = new Vector3(
            Mathf.Max(0.1f, wb.size.x / sx),
            Mathf.Max(0.1f, wb.size.y / sy),
            Mathf.Max(0.1f, wb.size.z / sz));
    }

    // ---------------------------------------------------------------
    //   PROYECTIL CUQUI (mismo FBX de la cuqui, escalado a tamano bala)
    // ---------------------------------------------------------------
    private static GameObject CreateCuquiProjectilePrefab()
    {
        string fbxPath = $"{PU_DIR}/Cuquis.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) { Debug.LogWarning($"[PowerUpSetup] No se encontro {fbxPath}"); return null; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (inst == null) return null;
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;

        FitScaleToMaxSize(inst, 0.45f);   // tamano de proyectil

        var sc = inst.GetComponent<SphereCollider>();
        if (sc == null) sc = inst.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        FitSphere(inst, sc);

        if (inst.GetComponent<CuquiProjectile>() == null)
            inst.AddComponent<CuquiProjectile>();   // [RequireComponent] añade el Rigidbody

        string prefabPath = $"{PREFAB_DIR}/Cuqui_Projectile.prefab";
        var asset = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        return asset;
    }

    private static void FitScaleToMaxSize(GameObject go, float targetMax)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maxDim < 1e-4f) return;
        go.transform.localScale *= targetMax / maxDim;
    }

    private static void FitSphere(GameObject go, SphereCollider sc)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { sc.radius = 0.25f; return; }
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        sc.center = go.transform.InverseTransformPoint(b.center);
        float worldR = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        float scale = Mathf.Abs(go.transform.lossyScale.x);
        if (scale < 1e-4f) scale = 1f;
        sc.radius = Mathf.Max(0.05f, worldR / scale);
    }

    // ---------------------------------------------------------------
    //   PLAYER
    // ---------------------------------------------------------------
    private static bool EnsurePlayerComponents(GameObject cuquiProjectile)
    {
        if (!System.IO.File.Exists(PLAYER_PREFAB)) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB);
        if (root == null) return false;

        var pc = root.GetComponent<PowerUpController>();
        if (pc == null) pc = root.AddComponent<PowerUpController>();
        if (root.GetComponent<PlayerSizeModifier>() == null) root.AddComponent<PlayerSizeModifier>();

        // Config canonica (valores pedidos): cuqui FBX, 1 disparo/3s, muzzle a la
        // altura de la mano, y tamano 15s + espera 15s.
        var so = new SerializedObject(pc);
        SetObj(so,  "cuquiProjectilePrefab", cuquiProjectile);
        SetFloat(so, "cuquiCooldown", 3f);
        SetVec3(so, "cuquiMuzzleOffset", new Vector3(0.22f, 0.05f, 0f));
        SetFloat(so, "chipsDuration", 15f);
        SetFloat(so, "chipsCooldown", 15f);
        SetFloat(so, "merengueDuration", 15f);
        SetFloat(so, "merengueCooldown", 15f);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        return true;
    }

    private static void SetObj(SerializedObject so, string prop, Object val)
    { var p = so.FindProperty(prop); if (p != null) p.objectReferenceValue = val; }
    private static void SetFloat(SerializedObject so, string prop, float val)
    { var p = so.FindProperty(prop); if (p != null) p.floatValue = val; }
    private static void SetVec3(SerializedObject so, string prop, Vector3 val)
    { var p = so.FindProperty(prop); if (p != null) p.vector3Value = val; }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Code/prefabs"))
            AssetDatabase.CreateFolder("Assets/Code", "prefabs");
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
            AssetDatabase.CreateFolder("Assets/Code/prefabs", "PowerUps");
    }
}
#endif
