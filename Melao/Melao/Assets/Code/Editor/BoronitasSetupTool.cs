#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Configura layers, colliders y scripts en la escena activa para el Nivel 3
// (Boronitas). Reentrante (correrla 2 veces no duplica) y con Undo (Ctrl+Z).
//
// Menu: Tools > Melao > Setup Nivel 3 (Boronitas)
//
// Reglas (por prefijo de nombre / numero entre parentesis):
//   Wafer*, Piazza*, Churro*           -> Wall
//   BrazoReina*                        -> Wall, EXCEPTO BrazoReina (2) -> kill
//   MilHojas*                          -> Ground, EXCEPTO MilHojas (5) -> Ground + kill-desde-arriba
//   Roscon*, Mantecada*, Lengua*, solterita* -> Ground
//   Saltin*                            -> spikes (trigger + HazardKill)
//   Bomba (none,2,4,5,12)              -> BombElevator (sube al tocar)
//   Bomba (6..11)                      -> BombMovingPlatform (sube/baja, intercaladas)
//   Bomba (13..18)                     -> BombFadePlatform (se desvanece y regenera)
//   Arequipe (sin numero) + plane      -> kill (lava)
//   Arequipe (1) + plane               -> StickyFloor (lento/pegajoso)
//   Gallelado*, Herpo*                 -> BouncyPlatform (saltarinas)
//   Player                             -> PlayerRespawn + material Pops (controles INVERTIDOS)
public static class BoronitasSetupTool
{
    private const int LAYER_GROUND   = 8;
    private const int LAYER_WALL     = 9;
    private const int LAYER_PLATFORM = 10;
    private const int LAYER_CHANGUA  = 11;
    private const int LAYER_DEFAULT  = 0;
    private const int LAYER_PLAYER   = 7;

    private const string POPS_MATERIAL_PATH =
        "Assets/Personajes/Texturas/standardSurface2.mat";

    private static readonly int[] BOMB_ELEVATOR_NUMBERS = { -1, 2, 4, 5, 12 }; // -1 = sin numero
    private static readonly (int min, int max) BOMB_MOVING_RANGE = (6, 11);
    private static readonly (int min, int max) BOMB_FADE_RANGE = (13, 18);

    private static readonly Regex ParenNumberRegex = new Regex(@"\((\d+)\)");

    [MenuItem("Tools/Melao/Setup Nivel 3 (Boronitas)")]
    public static void RunSetup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[BoronitasSetup] No hay escena activa.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Nivel 3 Boronitas");

        var roots = scene.GetRootGameObjects();
        var all = new List<GameObject>();
        foreach (var r in roots) CollectRecursive(r.transform, all);

        var stats = new Dictionary<string, int>();
        var movingBombs = new List<GameObject>();

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

            // --- BOMBAS ---
            if (lower.StartsWith("bomba"))
            {
                int n = ExtractParenNumber(go.name);
                if (BOMB_ELEVATOR_NUMBERS.Contains(n))
                {
                    SetupBombElevator(go);
                    Bump(stats, "Bomba Elevator");
                }
                else if (IsInRange(n, BOMB_MOVING_RANGE))
                {
                    SetupBombMoving(go);
                    movingBombs.Add(go);
                    Bump(stats, "Bomba Movil");
                }
                else if (IsInRange(n, BOMB_FADE_RANGE))
                {
                    SetupBombFade(go);
                    Bump(stats, "Bomba Fade");
                }
                continue;
            }

            // --- AREQUIPE ---
            if (lower.StartsWith("arequipe"))
            {
                int n = ExtractParenNumber(go.name);
                if (n == 1)
                {
                    SetupStickyArequipe(go);
                    Bump(stats, "Arequipe Pegajoso");
                }
                else
                {
                    SetupKillArequipe(go);
                    Bump(stats, "Arequipe Mortal");
                }
                continue;
            }

            // --- BRAZO REINA ---
            if (lower.StartsWith("brazoreina"))
            {
                int n = ExtractParenNumber(go.name);
                if (n == 2)
                {
                    SetupHazardTrigger(go, LAYER_CHANGUA);
                    Bump(stats, "BrazoReina Mortal");
                }
                else
                {
                    SetLayerRecursive(go, LAYER_WALL);
                    EnsureMeshColliders(go, convex: false, isTrigger: false);
                    Bump(stats, "BrazoReina (Wall)");
                }
                continue;
            }

            // --- MILHOJAS ---
            if (lower.StartsWith("milhojas"))
            {
                int n = ExtractParenNumber(go.name);
                SetLayerRecursive(go, LAYER_GROUND);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                if (n == 5)
                {
                    var hazards = AddComponentToColliderHosts<HazardKill>(go, solidOnly: true);
                    foreach (var h in hazards)
                    {
                        Undo.RecordObject(h, "Configure TopKill");
                        h.onlyKillFromTop = true;
                        h.playerLayer = 1 << LAYER_PLAYER;
                        EditorUtility.SetDirty(h);
                    }
                    Bump(stats, "MilHojas Trampa(5)");
                }
                else
                {
                    Bump(stats, "MilHojas (Ground)");
                }
                continue;
            }

            // --- SALTIN (spikes) ---
            if (lower.StartsWith("saltin"))
            {
                SetupHazardTrigger(go, LAYER_DEFAULT);
                Bump(stats, "Saltin (spikes)");
                continue;
            }

            // --- WALLS simples ---
            if (lower.StartsWith("wafer") || lower.StartsWith("piazza") || lower.StartsWith("churro"))
            {
                SetLayerRecursive(go, LAYER_WALL);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                Bump(stats, "Wall");
                continue;
            }

            // --- GROUND simples ---
            if (lower.StartsWith("roscon") || lower.StartsWith("mantecada") ||
                lower.StartsWith("lengua") || lower.StartsWith("solterita"))
            {
                SetLayerRecursive(go, LAYER_GROUND);
                EnsureMeshColliders(go, convex: false, isTrigger: false);
                Bump(stats, "Ground");
                continue;
            }

            // --- BOUNCE (Gallelado, Herpo) ---
            if (lower.StartsWith("gallelado") || lower.StartsWith("herpo"))
            {
                SetupBounce(go);
                Bump(stats, "Saltarina");
                continue;
            }
        }

        AssignMovingBombAlternation(movingBombs);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        string summary = string.Join("\n", stats.OrderBy(kv => kv.Key)
                                              .Select(kv => $"  {kv.Key}: {kv.Value}"));
        Debug.Log($"[BoronitasSetup] Listo.\n{summary}");
        EditorUtility.DisplayDialog(
            "Setup Nivel 3 Boronitas",
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
            Undo.RecordObject(pc, "Boronitas Player Config");
            pc.invertHorizontalInput = true; // Boronitas corre con controles invertidos
            EditorUtility.SetDirty(pc);
        }

        Material popsMat = AssetDatabase.LoadAssetAtPath<Material>(POPS_MATERIAL_PATH);
        if (popsMat == null) return;
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
    //   BOMBAS
    // ---------------------------------------------------------------
    private static void SetupBombElevator(GameObject go)
    {
        CleanBombComponents(go);
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        EnsureKinematicRigidbody(go);

        var elev = go.GetComponent<BombElevator>();
        if (elev == null) elev = Undo.AddComponent<BombElevator>(go);
        Undo.RecordObject(elev, "Configure BombElevator");
        elev.playerLayer = 1 << LAYER_PLAYER;
        elev.stopLayers = StopMask();
        EditorUtility.SetDirty(elev);
    }

    private static void SetupBombMoving(GameObject go)
    {
        CleanBombComponents(go);
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        EnsureKinematicRigidbody(go);

        var mover = go.GetComponent<BombMovingPlatform>();
        if (mover == null) mover = Undo.AddComponent<BombMovingPlatform>(go);
        Undo.RecordObject(mover, "Configure BombMovingPlatform");
        mover.obstacleLayer = ObstacleMask();
        EditorUtility.SetDirty(mover);
    }

    private static void SetupBombFade(GameObject go)
    {
        CleanBombComponents(go);
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: false, isTrigger: false);
        // Las fade NO llevan Rigidbody (son estaticas hasta desvanecer).
        DestroyIfExists<Rigidbody>(go);

        var fade = go.GetComponent<BombFadePlatform>();
        if (fade == null) fade = Undo.AddComponent<BombFadePlatform>(go);
        Undo.RecordObject(fade, "Configure BombFadePlatform");
        fade.playerLayer = 1 << LAYER_PLAYER;
        EditorUtility.SetDirty(fade);
    }

    // Quita componentes de otros tipos de bomba (al re-correr el tool y cambiar categoria).
    private static void CleanBombComponents(GameObject go)
    {
        DestroyIfExists<BombElevator>(go);
        DestroyIfExists<BombMovingPlatform>(go);
        DestroyIfExists<BombFadePlatform>(go);
    }

    private static void AssignMovingBombAlternation(List<GameObject> bombs)
    {
        if (bombs.Count == 0) return;
        // Ordena por X y, si estan alineadas en X, por Y. Asi quedan
        // "intercaladas" tanto en fila horizontal como en columna vertical.
        var sorted = bombs
            .OrderBy(g => Mathf.Round(g.transform.position.x * 10f) / 10f)
            .ThenBy(g => g.transform.position.y)
            .ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var mover = sorted[i].GetComponent<BombMovingPlatform>();
            if (mover == null) continue;
            Undo.RecordObject(mover, "Bomb Alternation");
            mover.invertDirection = (i % 2) == 1;
            EditorUtility.SetDirty(mover);
        }
    }

    // ---------------------------------------------------------------
    //   AREQUIPE
    // ---------------------------------------------------------------
    private static void SetupKillArequipe(GameObject go)
    {
        // Lava: trigger + HazardKill en cada host de collider. Limpieza de sticky.
        foreach (var s in go.GetComponentsInChildren<StickyFloor>(true)) DestroyComp(s);

        SetLayerRecursive(go, LAYER_CHANGUA);
        EnsureMeshColliders(go, convex: true, isTrigger: true);
        var hazards = AddComponentToColliderHosts<HazardKill>(go, solidOnly: false);
        foreach (var h in hazards)
        {
            Undo.RecordObject(h, "Configure Arequipe Kill");
            h.onlyKillFromTop = false;
            h.playerLayer = 1 << LAYER_PLAYER;
            EditorUtility.SetDirty(h);
        }
    }

    private static void SetupStickyArequipe(GameObject go)
    {
        // Pegajoso: collider SOLIDO + StickyFloor en cada host. Limpieza de hazard.
        foreach (var h in go.GetComponentsInChildren<HazardKill>(true)) DestroyComp(h);

        SetLayerRecursive(go, LAYER_GROUND);
        EnsureMeshColliders(go, convex: false, isTrigger: false);
        var stickies = AddComponentToColliderHosts<StickyFloor>(go, solidOnly: true);
        foreach (var s in stickies)
        {
            Undo.RecordObject(s, "Configure StickyFloor");
            s.playerLayer = 1 << LAYER_PLAYER;
            EditorUtility.SetDirty(s);
        }
    }

    // ---------------------------------------------------------------
    //   HAZARD GENERICO (saltin, brazoreina(2))
    // ---------------------------------------------------------------
    private static void SetupHazardTrigger(GameObject go, int layer)
    {
        SetLayerRecursive(go, layer);
        EnsureMeshColliders(go, convex: true, isTrigger: true);
        var hazards = AddComponentToColliderHosts<HazardKill>(go, solidOnly: false);
        foreach (var h in hazards)
        {
            Undo.RecordObject(h, "Configure Hazard");
            h.onlyKillFromTop = false;
            h.playerLayer = 1 << LAYER_PLAYER;
            EditorUtility.SetDirty(h);
        }
    }

    // ---------------------------------------------------------------
    //   BOUNCE (Gallelado, Herpo)
    // ---------------------------------------------------------------
    private static void SetupBounce(GameObject go)
    {
        SetLayerRecursive(go, LAYER_PLATFORM);
        EnsureMeshColliders(go, convex: true, isTrigger: false);
        // BouncyPlatform usa OnCollisionEnter, que se dispara en el GameObject
        // del collider. Si la malla esta en un hijo, el script debe ir ahi.
        AddComponentToColliderHosts<BouncyPlatform>(go, solidOnly: true);
    }

    // ---------------------------------------------------------------
    //   HELPERS
    // ---------------------------------------------------------------
    private static int LayerBit(int l) => 1 << l;
    private static LayerMask ObstacleMask() =>
        LayerBit(LAYER_GROUND) | LayerBit(LAYER_WALL) | LayerBit(LAYER_PLATFORM) | LayerBit(LAYER_CHANGUA);
    private static LayerMask StopMask() => ObstacleMask();

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

    // Agrega T a cada GameObject del subarbol que tenga un Collider. Si
    // solidOnly = true, ignora colliders trigger. Devuelve los componentes.
    private static List<T> AddComponentToColliderHosts<T>(GameObject root, bool solidOnly) where T : Component
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

    private static void DestroyIfExists<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) Undo.DestroyObjectImmediate(c);
    }

    private static void DestroyComp(Component c)
    {
        if (c != null) Undo.DestroyObjectImmediate(c);
    }

    private static int ExtractParenNumber(string name)
    {
        var m = ParenNumberRegex.Match(name);
        if (!m.Success) return -1;
        return int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
    }

    private static bool IsInRange(int n, (int min, int max) range)
    {
        return n >= range.min && n <= range.max;
    }

    private static void Bump(Dictionary<string, int> d, string key)
    {
        if (!d.ContainsKey(key)) d[key] = 0;
        d[key]++;
    }
}
#endif
