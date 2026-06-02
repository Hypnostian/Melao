#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Configura el Nivel 2 (Mas-melito). Reentrante + Undo (Ctrl+Z).
// Menu: Tools > Melao > Setup Nivel 2 (Mas-melito)
//
// Reglas:
//   Conito*                          -> pinchos (trigger + HazardKill)
//   GelatinaColores*, GetaPata*, Splass* -> Ground
//   Bocacdillo*                      -> Ground, EXCEPTO Bocacdillo (2) -> Wall
//   Torta3Leches (none,7,8,10,16,20,21,22,25,31,33,34,35,36,39,40) -> Ground; resto -> Wall
//   Drulas*, Pluck*                  -> Wall
//   Natilla*, JELLYBIG*              -> BouncyPlatform (impulsa salto); Natilla ademas material SOLIDO
//   Arroz con leche* + plane         -> liquido mortal (Changua) + Arrocito (olas) + textura
//   Mamut*, SugarAlgodon*, BiriBOM*  -> BalancePlatform (equilibrio)
//   Player                           -> PlayerRespawn
//   Main Camera                      -> CameraStraightener + nivelar roll (vista recta)
public static class MasMelitoSetupTool
{
    private const int LAYER_GROUND   = 8;
    private const int LAYER_WALL     = 9;
    private const int LAYER_PLATFORM = 10;
    private const int LAYER_CHANGUA  = 11;
    private const int LAYER_DEFAULT  = 0;
    private const int LAYER_PLAYER   = 7;

    private const string DIR = "Assets/Scenes/Mas-melito";
    private const string TEX_NATILLA_GUID = "45e1c724c599d744489e974d70c6dcbc";
    private const string TEX_ARROZ_GUID   = "6062259d76071194b9e4adca788059e9";

    private static readonly HashSet<int> TORTA_GROUND = new HashSet<int>
    { -1, 7, 8, 10, 16, 20, 21, 22, 25, 31, 33, 34, 35, 36, 39, 40 };

    private static readonly Regex ParenNumberRegex = new Regex(@"\((\d+)\)");

    [MenuItem("Tools/Melao/Setup Nivel 2 (Mas-melito)")]
    public static void RunSetup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[MasMelito] No hay escena activa."); return; }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Nivel 2 Mas-melito");

        Material natillaMat = GetOrCreateOpaqueMaterial($"{DIR}/Natilla_Solid.mat", TEX_NATILLA_GUID);
        Material arrozMat   = GetOrCreateOpaqueMaterial($"{DIR}/Arroz_Liquid.mat", TEX_ARROZ_GUID);

        // El mesh del Arroz necesita Read/Write para que Arrocito (olas) lo deforme.
        EnableReadWrite($"{DIR}/Arroz con leche.fbx");

        var roots = scene.GetRootGameObjects();
        var all = new List<GameObject>();
        foreach (var r in roots) CollectRecursive(r.transform, all);

        var stats = new Dictionary<string, int>();

        foreach (var go in all)
        {
            if (go == null) continue;
            string lower = go.name.ToLowerInvariant();

            if (lower == "player" || go.GetComponent<PlayerController2_5D>() != null)
            { SetupPlayer(go); Bump(stats, "Player"); continue; }

            if (lower.StartsWith("conito"))
            { SetupSpike(go); Bump(stats, "Conito (pinchos)"); continue; }

            if (lower.StartsWith("arroz"))
            { SetupArroz(go, arrozMat); Bump(stats, "Arroz (liquido)"); continue; }

            if (lower.StartsWith("natilla"))
            { SetupNatilla(go, natillaMat); Bump(stats, "Natilla (bounce)"); continue; }

            if (lower.StartsWith("jellybig"))
            { SetupBounce(go); Bump(stats, "JellyBig (bounce)"); continue; }

            if (lower.StartsWith("tamarindo"))
            { SetupBounce(go); Bump(stats, "Tamarindo (bounce)"); continue; }

            if (lower.StartsWith("mamut") || lower.StartsWith("sugaralgodon") || lower.StartsWith("biribom"))
            { SetupBalance(go); Bump(stats, "Balance"); continue; }

            if (lower.StartsWith("drulas") || lower.StartsWith("pluck"))
            { SetWallSolid(go); Bump(stats, "Wall"); continue; }

            if (lower.StartsWith("bocacdillo"))
            {
                int n = ExtractParenNumber(go.name);
                if (n == 2) { SetWallSolid(go); Bump(stats, "Bocacdillo(2) Wall"); }
                else { SetGroundSolid(go); Bump(stats, "Bocacdillo Ground"); }
                continue;
            }

            if (lower.StartsWith("torta3leches"))
            {
                int n = ExtractParenNumber(go.name);
                if (TORTA_GROUND.Contains(n)) { SetGroundSolid(go); Bump(stats, "Torta Ground"); }
                else { SetWallSolid(go); Bump(stats, "Torta Wall"); }
                continue;
            }

            if (lower.StartsWith("gelatina") || lower.StartsWith("getapata") || lower.StartsWith("splass"))
            { SetGroundSolid(go); Bump(stats, "Ground"); continue; }
        }

        SetupCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undo);

        string summary = string.Join("\n", stats.OrderBy(kv => kv.Key).Select(kv => $"  {kv.Key}: {kv.Value}"));
        Debug.Log($"[MasMelito] Listo.\n{summary}");
        EditorUtility.DisplayDialog("Setup Nivel 2 Mas-melito",
            $"Configuracion aplicada:\n\n{summary}\n\n" +
            "Camara nivelada (sin diagonal). Guarda con Ctrl+S.",
            "OK");
    }

    // ---------------------------------------------------------------
    private static void SetupPlayer(GameObject go)
    {
        if (go.GetComponent<PlayerRespawn>() == null)
            Undo.AddComponent<PlayerRespawn>(go);
        var pc = go.GetComponent<PlayerController2_5D>();
        // Mas-melito: controles invertidos (solo en este nivel).
        if (pc != null) { Undo.RecordObject(pc, "Player"); pc.invertHorizontalInput = true; EditorUtility.SetDirty(pc); }
    }

    private static void SetupSpike(GameObject go)
    {
        SetLayerRecursive(go, LAYER_DEFAULT);
        EnsureMeshColliders(go, convex: true, isTrigger: true);
        foreach (var h in AddToColliderHosts<HazardKill>(go, false)) ConfigHazard(h, false, lethal: false); // pinchos: 1 corazon + empuje
    }

    private static void SetWallSolid(GameObject go)
    {
        SetLayerRecursive(go, LAYER_WALL);
        EnsureMeshColliders(go, convex: false, isTrigger: false);
    }

    private static void SetGroundSolid(GameObject go)
    {
        SetLayerRecursive(go, LAYER_GROUND);
        EnsureMeshColliders(go, convex: false, isTrigger: false);
    }

    private static void SetupNatilla(GameObject go, Material solidMat)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        AddToColliderHosts<BouncyPlatform>(go, true);
        // Material solido (no transparente).
        if (solidMat != null)
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(r, "Natilla Material");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = solidMat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
    }

    private static void SetupBounce(GameObject go)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        AddToColliderHosts<BouncyPlatform>(go, true);
    }

    private static void SetupBalance(GameObject go)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        EnsureKinematicRigidbody(go);
        // Script UNICO integrado (balancin + deslizar + boost de salto).
        // Limpiar los componentes separados viejos al re-correr.
        DestroyAll<BalancePlatform>(go);
        DestroyAll<BouncyPlatform>(go);
        if (go.GetComponent<SeesawPlatform>() == null)
            Undo.AddComponent<SeesawPlatform>(go);
    }

    private static void DestroyAll<T>(GameObject go) where T : Component
    {
        foreach (var c in go.GetComponentsInChildren<T>(true))
            if (c != null) Undo.DestroyObjectImmediate(c);
    }

    private static void SetupArroz(GameObject go, Material arrozMat)
    {
        SetLayerRecursive(go, LAYER_CHANGUA);
        // Volumen de muerte: BoxCollider trigger que cubre el liquido y se
        // extiende hacia abajo (atrapa al jugador que cae). Mas fiable que un
        // MeshCollider convex de un plano plano.
        AddLiquidTrigger(go);
        var hz = go.GetComponent<HazardKill>();
        if (hz == null) hz = Undo.AddComponent<HazardKill>(go);
        ConfigHazard(hz, false, lethal: true);   // arroz con leche (liquido): mortal

        // En cada malla (el "plane" liquido): olas + textura arroz con leche.
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.GetComponent<Arrocito>() == null) Undo.AddComponent<Arrocito>(mf.gameObject);
            var r = mf.GetComponent<Renderer>();
            if (r != null && arrozMat != null)
            {
                Undo.RecordObject(r, "Arroz Material");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = arrozMat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }
    }

    // BoxCollider trigger que cubre el liquido (XZ) y se extiende hacia abajo.
    private static void AddLiquidTrigger(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        Undo.RecordObject(box, "Liquid Trigger");
        box.isTrigger = true;

        if (renderers.Length > 0)
        {
            Bounds wb = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) wb.Encapsulate(renderers[i].bounds);

            const float depth = 3f;
            Vector3 worldCenter = new Vector3(wb.center.x, wb.max.y - depth * 0.5f, wb.center.z);
            Vector3 localCenter = go.transform.InverseTransformPoint(worldCenter);
            Vector3 s = go.transform.lossyScale;
            float sx = Mathf.Max(1e-4f, Mathf.Abs(s.x));
            float sy = Mathf.Max(1e-4f, Mathf.Abs(s.y));
            float sz = Mathf.Max(1e-4f, Mathf.Abs(s.z));
            box.center = localCenter;
            box.size = new Vector3(
                Mathf.Max(wb.size.x, 0.5f) / sx,
                depth / sy,
                Mathf.Max(wb.size.z, 0.5f) / sz);
        }
        EditorUtility.SetDirty(box);
    }

    private static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var tagged = GameObject.FindWithTag("MainCamera");
            if (tagged != null) cam = tagged.GetComponent<Camera>();
        }
        if (cam == null) { Debug.LogWarning("[MasMelito] No se encontro Main Camera."); return; }

        if (cam.GetComponent<CameraStraightener>() == null)
            Undo.AddComponent<CameraStraightener>(cam.gameObject);

        // Nivelar el roll en edit-time (la vista deja de verse diagonal).
        Undo.RecordObject(cam.transform, "Level Camera");
        Vector3 e = cam.transform.eulerAngles;
        e.z = 0f;
        cam.transform.eulerAngles = e;
        EditorUtility.SetDirty(cam.transform);
    }

    // Activa Read/Write en el mesh del FBX (necesario para deformarlo en runtime).
    private static void EnableReadWrite(string fbxPath)
    {
        var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"[MasMelito] No importer en {fbxPath}"); return; }
        if (!mi.isReadable)
        {
            mi.isReadable = true;
            mi.SaveAndReimport();
        }
    }

    // ---------------------------------------------------------------
    //   MATERIALES
    // ---------------------------------------------------------------
    private static Material GetOrCreateOpaqueMaterial(string matPath, string textureGuid)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        string texPath = AssetDatabase.GUIDToAssetPath(textureGuid);
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        // Forzar OPACO (la natilla se veia transparente).
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 1);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------
    //   HELPERS
    // ---------------------------------------------------------------
    private static void ConfigHazard(HazardKill h, bool fromTop, bool lethal)
    {
        Undo.RecordObject(h, "Configure Hazard");
        h.onlyKillFromTop = fromTop;
        h.lethal = lethal;
        h.playerLayer = 1 << LAYER_PLAYER;
        EditorUtility.SetDirty(h);
    }

    private static void CollectRecursive(Transform t, List<GameObject> bag)
    {
        bag.Add(t.gameObject);
        for (int i = 0; i < t.childCount; i++) CollectRecursive(t.GetChild(i), bag);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        Undo.RecordObject(go, "Set Layer");
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    private static void EnsureKinematicRigidbody(GameObject go)
    {
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);
        Undo.RecordObject(rb, "Rigidbody");
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        EditorUtility.SetDirty(rb);
    }

    private static void EnsureMeshColliders(GameObject root, bool convex, bool isTrigger)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0)
        {
            if (root.GetComponent<Collider>() == null)
            { var box = Undo.AddComponent<BoxCollider>(root); box.isTrigger = isTrigger; }
            return;
        }
        foreach (var mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            var go = mf.gameObject;
            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc == null)
            {
                if (go.GetComponent<Collider>() != null && go == root) continue;
                mc = Undo.AddComponent<MeshCollider>(go);
            }
            Undo.RecordObject(mc, "MeshCollider");
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = convex;
            mc.isTrigger = isTrigger;
            EditorUtility.SetDirty(mc);
        }
    }

    private static List<T> AddToColliderHosts<T>(GameObject root, bool solidOnly) where T : Component
    {
        var result = new List<T>();
        var colliders = root.GetComponentsInChildren<Collider>(true);
        var seen = new HashSet<GameObject>();
        foreach (var c in colliders)
        {
            if (c == null) continue;
            if (solidOnly && c.isTrigger) continue;
            var host = c.gameObject;
            if (!seen.Add(host)) continue;
            var comp = host.GetComponent<T>();
            if (comp == null) comp = Undo.AddComponent<T>(host);
            result.Add(comp);
        }
        return result;
    }

    private static int ExtractParenNumber(string name)
    {
        var m = ParenNumberRegex.Match(name);
        if (!m.Success) return -1;
        return int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
    }

    private static void Bump(Dictionary<string, int> d, string key)
    {
        if (!d.ContainsKey(key)) d[key] = 0;
        d[key]++;
    }
}
#endif
