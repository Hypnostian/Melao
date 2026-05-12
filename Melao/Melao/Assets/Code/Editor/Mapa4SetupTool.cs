#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Herramienta one-shot que configura layers, colliders y scripts en la escena
// activa para el Mapa 4 (Choco-Lala). Reentrante: ejecutarla dos veces no
// duplica componentes. Todo pasa por Undo, asi que Ctrl+Z revierte.
//
// Uso:
//   Menu: Tools > Melao > Setup Mapa 4 (Chocolala)
public static class Mapa4SetupTool
{
    private const int LAYER_GROUND   = 8;
    private const int LAYER_WALL     = 9;
    private const int LAYER_PLATFORM = 10;
    private const int LAYER_CHANGUA  = 11;
    private const int LAYER_DEFAULT  = 0;
    private const int LAYER_PLAYER   = 7;

    private const string POPS_MATERIAL_PATH =
        "Assets/Personajes/Texturas/standardSurface2.mat";

    // Rangos de "gol" que se comportan como pared estatica (no matan, no mueven).
    // Usados como soporte para wall jump.
    private static readonly (int min, int max)[] WALL_GOL_RANGES = {
        (4, 10),
        (17, 23),
    };

    // Rangos de "Moneda" que se comportan como plataforma-elevador.
    private static readonly (int min, int max)[] ELEVATOR_MONEDA_RANGES = {
        (4, 8),
    };

    private static readonly Regex ParenNumberRegex = new Regex(@"\((\d+)\)");

    [MenuItem("Tools/Melao/Setup Mapa 4 (Chocolala)")]
    public static void RunSetup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[Mapa4Setup] No hay escena activa.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Mapa 4 Chocolala");

        var roots = scene.GetRootGameObjects();
        var all = new List<GameObject>();
        foreach (var r in roots) CollectRecursive(r.transform, all);

        var stats = new Dictionary<string, int>();
        var gols = new List<GameObject>();

        foreach (var go in all)
        {
            if (go == null) continue;
            string lower = go.name.ToLowerInvariant();

            // Player
            if (lower == "player" || go.GetComponent<PlayerController2_5D>() != null)
            {
                SetupPlayer(go);
                Bump(stats, "Player");
                continue;
            }

            // -- HIJOS ESPECIFICOS DE NUCITA --
            // Solo se procesan si tienen "nucita" en algun ancestro.
            if (HasAncestorContaining(go.transform, "nucita"))
            {
                if (lower.Contains("pcube6"))
                {
                    SetupChanguaHazard(go);
                    Bump(stats, "Nucita Relleno (pCube6)");
                    continue;
                }
                if (lower.Contains("pcylinder"))
                {
                    SetLayerRecursive(go, LAYER_GROUND);
                    EnsureMeshColliders(go, convex: false, isTrigger: false);
                    Bump(stats, "Nucita Pilar (Ground)");
                    continue;
                }
            }

            // Gol (separar en moviles vs walls)
            if (lower.StartsWith("gol"))
            {
                int n = ExtractParenNumber(go.name);
                if (IsInAnyRange(n, WALL_GOL_RANGES))
                {
                    SetupGolAsWall(go);
                    Bump(stats, "Gol (Wall)");
                }
                else
                {
                    SetupGol(go);
                    gols.Add(go);
                    Bump(stats, "Gol (movil)");
                }
                continue;
            }

            // QuesoPUNTEAGUDO (chequear antes que generico "queso/quesito")
            if (lower.Contains("punteagudo"))
            {
                SetupHazardTrigger(go, LAYER_DEFAULT);
                Bump(stats, "QuesoPUNTEAGUDO");
                continue;
            }

            // Changua (lava) - ahora en su propia capa
            if (lower.StartsWith("changua"))
            {
                SetupChanguaHazard(go);
                Bump(stats, "Changua");
                continue;
            }

            // Baloncito
            if (lower.StartsWith("baloncito"))
            {
                SetupBaloncito(go);
                Bump(stats, "Baloncito");
                continue;
            }

            // Pingu
            if (lower.StartsWith("pingu"))
            {
                SetupPingu(go);
                Bump(stats, "Pingu");
                continue;
            }

            // ChocoBREAK: root es ground, relleno (pasted__pCube3) es volcan
            if (lower.StartsWith("chocobreak"))
            {
                SetLayerRecursive(go, LAYER_GROUND);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                var relleno = FindRellenoChild(go.transform);
                if (relleno != null) SetupVolcano(relleno.gameObject);
                Bump(stats, "ChocoBREAK");
                continue;
            }

            // Choquito -> Wall
            if (lower.StartsWith("choquito"))
            {
                SetLayerRecursive(go, LAYER_WALL);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                Bump(stats, "Choquito (Wall)");
                continue;
            }

            // Chocolatina con mani -> Wall
            if (lower.StartsWith("chocolatina con mani"))
            {
                SetLayerRecursive(go, LAYER_WALL);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                Bump(stats, "Chocolatina (Wall)");
                continue;
            }

            // Moneda: separar elevator vs ground normal
            if (lower.StartsWith("moneda"))
            {
                int n = ExtractParenNumber(go.name);
                if (IsInAnyRange(n, ELEVATOR_MONEDA_RANGES))
                {
                    SetupElevatorMoneda(go);
                    Bump(stats, "Moneda Elevator");
                }
                else
                {
                    SetLayerRecursive(go, LAYER_GROUND);
                    EnsureMeshColliders(go, convex: false, isTrigger: false);
                    Bump(stats, "Moneda (Ground)");
                }
                continue;
            }

            // Ground caminable
            if (lower.StartsWith("paredes") ||
                lower.StartsWith("pisooooo") ||
                lower.StartsWith("puente") ||
                lower.StartsWith("oria") ||
                lower.StartsWith("galleta") ||
                lower.StartsWith("tostadito") ||
                lower.StartsWith("nucita") ||
                (lower.StartsWith("quesito") && !lower.Contains("punteagudo")))
            {
                SetLayerRecursive(go, LAYER_GROUND);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                Bump(stats, "Ground");
                continue;
            }
        }

        AssignGolPairs(gols);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        string summary = string.Join("\n", stats.OrderBy(kv => kv.Key)
                                              .Select(kv => $"  {kv.Key}: {kv.Value}"));
        Debug.Log($"[Mapa4Setup] Listo.\n{summary}");
        EditorUtility.DisplayDialog(
            "Setup Mapa 4 Chocolala",
            $"Configuracion aplicada:\n\n{summary}\n\n" +
            "Guarda la escena con Ctrl+S. Usa Ctrl+Z para revertir todo.",
            "OK");
    }

    // ---------------------------------------------------------------
    //   PLAYER
    // ---------------------------------------------------------------
    private static void SetupPlayer(GameObject go)
    {
        if (go.GetComponent<PlayerRespawn>() == null)
            Undo.AddComponent<PlayerRespawn>(go);

        var pc = go.GetComponent<PlayerController2_5D>();
        if (pc != null)
        {
            Undo.RecordObject(pc, "Invert Horizontal Input (Chocolala)");
            pc.invertHorizontalInput = true;
            EditorUtility.SetDirty(pc);
        }

        Material popsMat = AssetDatabase.LoadAssetAtPath<Material>(POPS_MATERIAL_PATH);
        if (popsMat == null)
        {
            Debug.LogWarning($"[Mapa4Setup] No se encontro material en '{POPS_MATERIAL_PATH}'.");
            return;
        }

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Undo.RecordObject(r, "Apply Pops Material");
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = popsMat;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
        }
    }

    // ---------------------------------------------------------------
    //   GOL movil (plataforma vertical mortal kinematica)
    // ---------------------------------------------------------------
    private static void SetupGol(GameObject go)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        EnsureKinematicRigidbody(go);

        var mover = go.GetComponent<GolPairMover>();
        if (mover == null) mover = Undo.AddComponent<GolPairMover>(go);
        Undo.RecordObject(mover, "Configure GolPairMover");
        // Forzar mascara correcta aunque ya existiera con valores antiguos.
        mover.obstacleLayer = (1 << LAYER_GROUND) | (1 << LAYER_WALL) | (1 << LAYER_CHANGUA);
        EditorUtility.SetDirty(mover);

        if (go.GetComponent<HazardKill>() == null)
            Undo.AddComponent<HazardKill>(go);

        if (go.GetComponent<MovingPlatformMotionSender>() == null)
            Undo.AddComponent<MovingPlatformMotionSender>(go);
    }

    // ---------------------------------------------------------------
    //   GOL como pared estatica (para wall jump)
    // ---------------------------------------------------------------
    private static void SetupGolAsWall(GameObject go)
    {
        // Quitar componentes de gol movil si los tenia (al re-correr el tool).
        DestroyIfExists<GolPairMover>(go);
        DestroyIfExists<HazardKill>(go);
        DestroyIfExists<MovingPlatformMotionSender>(go);
        DestroyIfExists<Rigidbody>(go);

        SetLayerRecursive(go, LAYER_WALL);
        EnsureMeshColliders(go, convex: false, isTrigger: false);
    }

    // ---------------------------------------------------------------
    //   BALONCITO
    // ---------------------------------------------------------------
    private static void SetupBaloncito(GameObject go)
    {
        SetLayerRecursive(go, LAYER_DEFAULT);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        EnsureKinematicRigidbody(go);

        var roller = go.GetComponent<BaloncitoRoller>();
        if (roller == null) roller = Undo.AddComponent<BaloncitoRoller>(go);
        Undo.RecordObject(roller, "Configure BaloncitoRoller");
        // Forzar mascara: Ground + Wall + Platform + Changua.
        roller.obstacleLayer = (1 << LAYER_GROUND) | (1 << LAYER_WALL) |
                               (1 << LAYER_PLATFORM) | (1 << LAYER_CHANGUA);
        EditorUtility.SetDirty(roller);

        if (go.GetComponent<HazardKill>() == null)
            Undo.AddComponent<HazardKill>(go);
    }

    // ---------------------------------------------------------------
    //   PINGU
    // ---------------------------------------------------------------
    private static void SetupPingu(GameObject go)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);

        if (go.GetComponent<BouncyPlatform>() == null)
            Undo.AddComponent<BouncyPlatform>(go);
    }

    // ---------------------------------------------------------------
    //   MONEDA ELEVATOR
    // ---------------------------------------------------------------
    private static void SetupElevatorMoneda(GameObject go)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        EnsureKinematicRigidbody(go);

        var elev = go.GetComponent<ElevatorMoneda>();
        if (elev == null) elev = Undo.AddComponent<ElevatorMoneda>(go);
        Undo.RecordObject(elev, "Configure ElevatorMoneda");
        elev.playerLayer = 1 << LAYER_PLAYER;
        elev.stopLayers = (1 << LAYER_GROUND) | (1 << LAYER_WALL) |
                          (1 << LAYER_PLATFORM) | (1 << LAYER_CHANGUA);
        EditorUtility.SetDirty(elev);

        if (go.GetComponent<MovingPlatformMotionSender>() == null)
            Undo.AddComponent<MovingPlatformMotionSender>(go);
    }

    // ---------------------------------------------------------------
    //   HAZARDS TRIGGER (queso punteagudo)
    // ---------------------------------------------------------------
    private static void SetupHazardTrigger(GameObject go, int layer)
    {
        SetLayerRecursive(go, layer);
        EnsureMeshColliders(go, convex: true, isTrigger: true);

        if (go.GetComponent<HazardKill>() == null)
            Undo.AddComponent<HazardKill>(go);
    }

    // Changua (y similares como nucita pCube6) en layer Changua.
    private static void SetupChanguaHazard(GameObject go)
    {
        SetupHazardTrigger(go, LAYER_CHANGUA);
    }

    // ---------------------------------------------------------------
    //   VOLCANO
    // ---------------------------------------------------------------
    private static void SetupVolcano(GameObject go)
    {
        var vol = go.GetComponent<VolcanoExploder>();
        if (vol == null) vol = Undo.AddComponent<VolcanoExploder>(go);
        Undo.RecordObject(vol, "Configure VolcanoExploder");
        // Forzar valores correctos aunque ya existiera con valores antiguos.
        vol.explosionRadius = 0.45f;
        vol.playerLayer = 1 << LAYER_PLAYER;
        EditorUtility.SetDirty(vol);
    }

    private static Transform FindRellenoChild(Transform root)
    {
        var queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            string n = t.name.ToLowerInvariant();
            if (t != root && (n.Contains("pcube3") || n.Contains("relleno")))
                return t;
            for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
        }
        return null;
    }

    // ---------------------------------------------------------------
    //   GOL PAIRING
    // ---------------------------------------------------------------
    private static void AssignGolPairs(List<GameObject> gols)
    {
        if (gols.Count == 0) return;
        var sorted = gols.OrderBy(g => g.transform.position.x).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var mover = sorted[i].GetComponent<GolPairMover>();
            if (mover == null) continue;
            Undo.RecordObject(mover, "Gol Pair Direction");
            mover.invertDirection = (i % 2) == 1;
            EditorUtility.SetDirty(mover);
        }
    }

    // ---------------------------------------------------------------
    //   HELPERS
    // ---------------------------------------------------------------
    private static void CollectRecursive(Transform t, List<GameObject> bag)
    {
        bag.Add(t.gameObject);
        for (int i = 0; i < t.childCount; i++)
            CollectRecursive(t.GetChild(i), bag);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        Undo.RecordObject(go, "Set Layer");
        go.layer = layer;
        foreach (Transform c in go.transform)
            SetLayerRecursive(c.gameObject, layer);
    }

    private static void EnsureKinematicRigidbody(GameObject go)
    {
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);
        Undo.RecordObject(rb, "Configure Rigidbody");
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        EditorUtility.SetDirty(rb);
    }

    private static void EnsureMeshColliders(GameObject root, bool convex, bool isTrigger)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
        if (filters.Length == 0)
        {
            if (root.GetComponent<Collider>() == null)
            {
                var box = Undo.AddComponent<BoxCollider>(root);
                box.isTrigger = isTrigger;
            }
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
            Undo.RecordObject(mc, "Configure MeshCollider");
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = convex;
            mc.isTrigger = isTrigger;
            EditorUtility.SetDirty(mc);
        }
    }

    private static void DestroyIfExists<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) Undo.DestroyObjectImmediate(c);
    }

    private static int ExtractParenNumber(string name)
    {
        var m = ParenNumberRegex.Match(name);
        if (!m.Success) return -1;
        return int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
    }

    private static bool IsInAnyRange(int n, (int min, int max)[] ranges)
    {
        if (n < 0) return false;
        for (int i = 0; i < ranges.Length; i++)
            if (n >= ranges[i].min && n <= ranges[i].max) return true;
        return false;
    }

    private static bool HasAncestorContaining(Transform t, string substring)
    {
        string s = substring.ToLowerInvariant();
        Transform cur = t.parent;
        while (cur != null)
        {
            if (cur.name.ToLowerInvariant().Contains(s)) return true;
            cur = cur.parent;
        }
        return false;
    }

    private static void Bump(Dictionary<string, int> d, string key)
    {
        if (!d.ContainsKey(key)) d[key] = 0;
        d[key]++;
    }
}
#endif
