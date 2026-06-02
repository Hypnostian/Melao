#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Coloca y CABLEA, en la escena activa, el rescate del prisionero y/o el fin de
// nivel, segun el nivel. Hace todo el wiring (referencias, componentes, colliders)
// que no se puede hacer a mano facil.
//
// USO:
//   1) Abre la escena del nivel.
//   2) (Opcional pero recomendado) selecciona en la jerarquia/escena la plataforma
//      donde apunta la flecha (donde termina el nivel). Se usa como ancla.
//   3) Menu: Tools > Melao > Setup Level Story (NPC + End)
//
// Mapeo (GDD + orden de build):
//   Nivel 1 Quipto      -> sin NPC; LevelEnd -> "Nivel 2 Mas-melito"
//   Nivel 2 Mas-melito  -> Tathi;   rescate -> "Nivel 3 Boronitas"
//   Nivel 3 Boronitas   -> Trigorio;rescate -> "Nivel 4 Choco-Lala"
//   Nivel 4 Choco-Lala  -> Cremi;   rescate -> creditos (next vacio)
//
// Requiere haber corrido antes Tools > Melao > Setup NPCs (crea los prefabs).
public static class LevelStorySetupTool
{
    private const string NPC_PREFAB_DIR = "Assets/Personajes/NPC";

    [MenuItem("Tools/Melao/Setup Level Story (NPC + End)")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[LevelStory] No hay escena activa."); return; }

        string lower = scene.name.ToLowerInvariant();
        string npc = null, next = null;
        if (lower.Contains("quipto"))        { npc = null;       next = "Nivel 2 Mas-melito"; }
        else if (lower.Contains("mas-melito")|| lower.Contains("melito")) { npc = "Tathi";    next = "Nivel 3 Boronitas"; }
        else if (lower.Contains("boronitas")){ npc = "Trigorio"; next = "Nivel 4 Choco-Lala"; }
        else if (lower.Contains("choco"))    { npc = "Cremi";    next = ""; /* creditos */ }
        else { Debug.LogWarning($"[LevelStory] Escena '{scene.name}' no reconocida."); }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Level Story");
        int group = Undo.GetCurrentGroup();

        Vector3 anchor = AnchorPosition();

        string report;
        if (string.IsNullOrEmpty(npc))
        {
            // Solo fin de nivel (Quipto).
            EnsureLevelEnd(scene.name, next, anchor);
            report = $"LevelEnd colocado.\n  Siguiente: {next}";
        }
        else
        {
            // Prisionero + cutscene de rescate (que ademas avanza de nivel).
            bool ok = SetupRescue(npc, scene.name, next, anchor);
            report = ok
                ? $"NPC '{npc}' + RescueCutscene colocados y cableados.\n" +
                  (string.IsNullOrEmpty(next) ? "  Al terminar: CREDITOS." : $"  Al terminar: {next}")
                : $"AVISO: no se encontro el prefab {NPC_PREFAB_DIR}/{npc}_NPC.prefab.\n  Corre primero Tools > Melao > Setup NPCs.";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);

        string msg = $"Escena: {scene.name}\n\n{report}\n\n" +
                     "Ajusta la posicion del objeto creado a la flecha si hace falta " +
                     "(esta en el ancla / seleccion). Guarda con Ctrl+S.";
        Debug.Log("[LevelStory] " + msg);
        EditorUtility.DisplayDialog("Setup Level Story", msg, "OK");
        Selection.activeGameObject = GameObject.Find(string.IsNullOrEmpty(npc) ? "LevelEnd" : $"Rescate_{npc}");
    }

    // Ancla: objeto seleccionado -> su posicion; si no, el Player; si no, origen.
    private static Vector3 AnchorPosition()
    {
        if (Selection.activeTransform != null) return Selection.activeTransform.position;
        var pc = Object.FindFirstObjectByType<PlayerController2_5D>();
        if (pc != null) return pc.transform.position + Vector3.right * 3f;
        return Vector3.zero;
    }

    // ---------------- RESCATE ----------------
    private static bool SetupRescue(string npcName, string sceneName, string next, Vector3 anchor)
    {
        string prefabPath = $"{NPC_PREFAB_DIR}/{npcName}_NPC.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return false;

        // Contenedor del rescate.
        string rootName = $"Rescate_{npcName}";
        var root = GameObject.Find(rootName);
        if (root == null)
        {
            root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Rescate");
        }
        root.transform.position = anchor;

        // NPC prisionero (instancia del prefab).
        NpcCharacter npcInst = root.GetComponentInChildren<NpcCharacter>();
        if (npcInst == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Spawn NPC");
            go.name = $"{npcName}_NPC";
            go.transform.SetParent(root.transform, true);
            npcInst = go.GetComponent<NpcCharacter>();
        }
        npcInst.transform.position = anchor;

        // Puntos de la cutscene.
        Transform stand = EnsureChild(root.transform, "PopsStand", anchor + new Vector3(-1.5f, 0f, 0f));
        Transform exit  = EnsureChild(root.transform, "RunExit",   anchor + new Vector3(4f, 0f, 0f));

        // Trigger de cutscene (zona donde Pops activa el rescate).
        var trig = root.GetComponent<RescueCutscene>();
        if (trig == null) trig = Undo.AddComponent<RescueCutscene>(root);
        var box = root.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(root);
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(3.5f, 4f, 4f);

        // Cableado por SerializedObject (campos privados [SerializeField]).
        var so = new SerializedObject(trig);
        SetObj(so, "npc", npcInst);
        SetObj(so, "popsStandPoint", stand);
        SetObj(so, "runExitPoint", exit);
        SetBool(so, "advanceLevelOnEnd", true);
        SetStr(so, "nextLevelName", next);
        SetStr(so, "currentLevelName", sceneName);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Si la escena tenia un LevelEndTrigger redundante en este punto, no lo toco;
        // el rescate maneja el avance. (Chocolala puede conservar su fin a creditos.)
        return true;
    }

    // ---------------- FIN DE NIVEL (Quipto) ----------------
    private static void EnsureLevelEnd(string sceneName, string next, Vector3 anchor)
    {
        var existing = Object.FindFirstObjectByType<LevelEndTrigger>();
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null)
        {
            go = new GameObject("LevelEnd");
            Undo.RegisterCreatedObjectUndo(go, "Create LevelEnd");
        }
        go.transform.position = anchor;

        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(3f, 4f, 4f);

        var trig = go.GetComponent<LevelEndTrigger>();
        if (trig == null) trig = Undo.AddComponent<LevelEndTrigger>(go);
        var so = new SerializedObject(trig);
        SetStr(so, "nextLevelName", next);
        SetStr(so, "currentLevelName", sceneName);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- helpers ----------------
    private static Transform EnsureChild(Transform parent, string name, Vector3 worldPos)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Point");
            go.transform.SetParent(parent, true);
            t = go.transform;
        }
        t.position = worldPos;
        return t;
    }

    private static void SetObj(SerializedObject so, string prop, Object val)
    { var p = so.FindProperty(prop); if (p != null) p.objectReferenceValue = val; }
    private static void SetBool(SerializedObject so, string prop, bool val)
    { var p = so.FindProperty(prop); if (p != null) p.boolValue = val; }
    private static void SetStr(SerializedObject so, string prop, string val)
    { var p = so.FindProperty(prop); if (p != null) p.stringValue = val ?? ""; }
}
#endif
