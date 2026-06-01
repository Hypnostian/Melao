#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Crea los Animator Controllers de los enemigos a partir de las animaciones en
// Assets/Animacion/Enemigos, configura el loop de los ciclos, y (al correr el
// menu) asigna cada controller al personaje correspondiente en la escena ABIERTA
// buscando por nombre.
//
// Menu: Tools > Melao > Setup Enemy Animators
//
// Mapeo confirmado:
//   Bonon      (Walk Cycle, Dash, Jump)            -> Bonon_texturizado
//   SSS        (idle, Run cycle, Warning, Crash)   -> Sir_Saladin_Texturizado
//   Yucat      (Idle, WalkCycle, Ataque)           -> Yucat_texturizado
//   Ñuelito    (Walk Cycle, Shooting)              -> Nuelito_texturizadp
//
// NOTA: las animaciones son Generic (atadas a la jerarquia de huesos del rig_X).
// Se asigna al Animator el Avatar PROPIO del modelo del personaje; los clips se
// aplican por ruta de hueso. Si algun personaje no anima bien, su esqueleto no
// coincide con el de su rig_X y habria que reexportar; avisar en consola.
public static class EnemyAnimatorSetup
{
    private const string ENEMY_DIR  = "Assets/Animacion/Enemigos";
    private const string CTRL_DIR   = "Assets/Animacion/Enemigos/Controllers";
    private const string CHAR_DIR   = "Assets/Personajes";
    private const string PREFAB_DIR = "Assets/Personajes/Enemigos";

    // Mapeo personaje -> (modelo en Personajes, nombre corto, builder de controller).
    private struct EnemyDef
    {
        public string model;       // archivo .fbx en Personajes (sin extension)
        public string shortName;   // nombre corto para prefab/controller
        public System.Func<AnimatorController> build;
        public string[] keywords;  // para localizarlo en escena
    }

    private static EnemyDef[] Defs()
    {
        return new[]
        {
            new EnemyDef { model = "Bonon_texturizado",       shortName = "Bonon",      build = BuildBonon,   keywords = new[]{ "bonon" } },
            new EnemyDef { model = "Sir_Saladin_Texturizado", shortName = "SirSaladin", build = BuildSaladin, keywords = new[]{ "saladin", "sir", "sss" } },
            new EnemyDef { model = "Yucat_texturizado",       shortName = "Yucat",      build = BuildYucat,   keywords = new[]{ "yucat" } },
            new EnemyDef { model = "Nuelito_texturizadp",     shortName = "Nuelito",    build = BuildNuelito, keywords = new[]{ "nuelito", "ñuelito" } },
        };
    }

    [MenuItem("Tools/Melao/Setup Enemy Animators")]
    public static void Run()
    {
        EnsureFolder();
        PrepareLoops();
        AssetDatabase.SaveAssets();

        // Cablear personajes en la escena abierta.
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Enemy Animators");
        int undo = Undo.GetCurrentGroup();

        int n = 0;
        foreach (var d in Defs())
            n += Wire(d.keywords, d.build(), d.model);

        Undo.CollapseUndoOperations(undo);

        Debug.Log($"[EnemyAnim] Controllers listos en {CTRL_DIR}. Personajes cableados en escena: {n}.");
        EditorUtility.DisplayDialog("Setup Enemy Animators",
            $"Animator Controllers creados:\n  Bonon, SirSaladin, Yucat, Ñuelito\n\n" +
            $"Personajes cableados en la escena abierta: {n}\n\n" +
            "Guarda la escena (Ctrl+S). Si algun personaje no estaba en la escena, " +
            "arrastra su controller manualmente desde " + CTRL_DIR + ".",
            "OK");
    }

    // Crea un PREFAB por personaje (modelo de Personajes + Animator con su
    // controller + avatar) en Assets/Personajes/Enemigos. Asi cada personaje de
    // la carpeta queda listo como enemigo, sin depender de una escena.
    [MenuItem("Tools/Melao/Create Enemy Prefabs (Personajes)")]
    public static void CreatePrefabs()
    {
        EnsureFolder();
        PrepareLoops();

        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
            AssetDatabase.CreateFolder(CHAR_DIR, "Enemigos");

        int n = 0;
        var made = new System.Text.StringBuilder();
        foreach (var d in Defs())
        {
            var ctrl = d.build();
            if (CreatePrefab(ctrl, d.model, d.shortName))
            {
                n++;
                made.Append("  ").Append(d.shortName).Append("\n");
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyAnim] {n} prefabs de enemigo creados en {PREFAB_DIR}.");
        EditorUtility.DisplayDialog("Create Enemy Prefabs",
            $"Prefabs creados en {PREFAB_DIR}:\n\n{made}\n" +
            "Cada uno tiene el modelo + Animator con su controller y avatar.\n" +
            "Arrastralos a la escena/nivel como enemigos.",
            "OK");
    }

    private static void PrepareLoops()
    {
        SetLoop("Bonon_ANI_Walk Cycle");
        SetLoop("SSS_Ani_idle");
        SetLoop("SSS_Ani_Run cycle");
        SetLoop("Yucat_ANI_Idle");
        SetLoop("Yucat_ANI_WalkCycle");
        SetLoop("Ñuelito_ANIWalk Cycle");
        AssetDatabase.Refresh();
    }

    private static bool CreatePrefab(AnimatorController ctrl, string modelFile, string prefabName)
    {
        string modelPath = $"{CHAR_DIR}/{modelFile}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) { Debug.LogWarning($"[EnemyAnim] No se encontro modelo {modelPath}"); return false; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (inst == null) { Debug.LogWarning($"[EnemyAnim] No se pudo instanciar {modelPath}"); return false; }

        var animator = inst.GetComponentInChildren<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        if (animator.avatar == null) animator.avatar = LoadAvatar(modelFile);
        animator.applyRootMotion = false;

        string prefabPath = $"{PREFAB_DIR}/{prefabName}_Enemy.prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        return true;
    }

    // -----------------------------------------------------------------
    //   CONTROLLERS
    // -----------------------------------------------------------------
    // Bonon: sin idle. Base = Walk (loop). Dash y Jump como one-shot.
    private static AnimatorController BuildBonon()
    {
        var ctrl = NewController("Bonon");
        ctrl.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var walk = AddState(sm, "Walk", Clip("Bonon_ANI_Walk Cycle"));
        sm.defaultState = walk;

        var dash = AddState(sm, "Dash", Clip("Bonon_ANI_Dash"));
        var jump = AddState(sm, "Jump", Clip("Bonon_ANI_Jump"));
        OneShot(sm, dash, walk, "Dash");
        OneShot(sm, jump, walk, "Jump");
        return ctrl;
    }

    // Sir Saladin (SSS): Idle <-> Run (Moving), Warning one-shot, Crash terminal.
    private static AnimatorController BuildSaladin()
    {
        var ctrl = NewController("SirSaladin");
        ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Warning", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Crash", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = AddState(sm, "Idle", Clip("SSS_Ani_idle"));
        var run  = AddState(sm, "Run",  Clip("SSS_Ani_Run cycle"));
        sm.defaultState = idle;
        Bool(idle, run, "Moving", true);
        Bool(run, idle, "Moving", false);

        var warning = AddState(sm, "Warning", Clip("SSS_Ani_Warning"));
        OneShot(sm, warning, idle, "Warning");

        var crash = AddState(sm, "Crash", Clip("SSS_Ani_Crash"));
        Trigger(sm, crash, "Crash"); // terminal (sin salida)
        return ctrl;
    }

    // Yucat: Idle <-> Walk (Moving), Ataque one-shot.
    private static AnimatorController BuildYucat()
    {
        var ctrl = NewController("Yucat");
        ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Ataque", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = AddState(sm, "Idle", Clip("Yucat_ANI_Idle"));
        var walk = AddState(sm, "Walk", Clip("Yucat_ANI_WalkCycle"));
        sm.defaultState = idle;
        Bool(idle, walk, "Moving", true);
        Bool(walk, idle, "Moving", false);

        var atk = AddState(sm, "Ataque", Clip("Yucat_ANI_Ataque"));
        OneShot(sm, atk, idle, "Ataque");
        return ctrl;
    }

    // Ñuelito: sin idle. Base = Walk (loop). Shooting one-shot.
    private static AnimatorController BuildNuelito()
    {
        var ctrl = NewController("Nuelito");
        ctrl.AddParameter("Shooting", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var walk = AddState(sm, "Walk", Clip("Ñuelito_ANIWalk Cycle"));
        sm.defaultState = walk;

        var shoot = AddState(sm, "Shooting", Clip("Ñuelito_ANIShooting"));
        OneShot(sm, shoot, walk, "Shooting");
        return ctrl;
    }

    // -----------------------------------------------------------------
    //   HELPERS de construccion
    // -----------------------------------------------------------------
    private static AnimatorController NewController(string name)
    {
        string path = $"{CTRL_DIR}/{name}_Enemy.controller";
        AssetDatabase.DeleteAsset(path); // reentrante: reconstruir limpio
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        var st = sm.AddState(name);
        st.motion = clip;
        st.writeDefaultValues = true;
        if (clip == null)
            Debug.LogWarning($"[EnemyAnim] Clip nulo para estado '{name}'. Revisa el FBX.");
        return st;
    }

    // Transicion AnyState -> target por trigger, y target -> returnTo por exit time.
    private static void OneShot(AnimatorStateMachine sm, AnimatorState target, AnimatorState returnTo, string trigger)
    {
        var inT = sm.AddAnyStateTransition(target);
        inT.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        inT.hasExitTime = false;
        inT.duration = 0.06f;
        inT.canTransitionToSelf = false;

        if (returnTo != null)
        {
            var outT = target.AddTransition(returnTo);
            outT.hasExitTime = true;
            outT.exitTime = 0.9f;
            outT.duration = 0.06f;
        }
    }

    // AnyState -> target por trigger, sin salida (estado terminal, ej. muerte).
    private static void Trigger(AnimatorStateMachine sm, AnimatorState target, string trigger)
    {
        var inT = sm.AddAnyStateTransition(target);
        inT.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        inT.hasExitTime = false;
        inT.duration = 0.06f;
        inT.canTransitionToSelf = false;
    }

    // src -> dst cuando el bool 'param' es 'value'.
    private static void Bool(AnimatorState src, AnimatorState dst, string param, bool value)
    {
        var t = src.AddTransition(dst);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    // -----------------------------------------------------------------
    //   CARGA de clips / avatares
    // -----------------------------------------------------------------
    private static AnimationClip Clip(string fileNoExt)
    {
        string path = $"{ENEMY_DIR}/{fileNoExt}.fbx";
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }
        Debug.LogWarning($"[EnemyAnim] No se encontro AnimationClip en {path}");
        return null;
    }

    private static Avatar LoadAvatar(string charFileNoExt)
    {
        string path = $"{CHAR_DIR}/{charFileNoExt}.fbx";
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Avatar av) return av;
        return null;
    }

    private static void SetLoop(string fileNoExt)
    {
        string path = $"{ENEMY_DIR}/{fileNoExt}.fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"[EnemyAnim] No importer en {path}"); return; }

        var clips = mi.clipAnimations;
        if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
            clips[i].loopTime = true;
        mi.clipAnimations = clips;
        EditorUtility.SetDirty(mi);
        mi.SaveAndReimport();
    }

    // -----------------------------------------------------------------
    //   CABLEADO EN ESCENA
    // -----------------------------------------------------------------
    private static int Wire(string[] keywords, AnimatorController ctrl, string charFileNoExt)
    {
        var avatar = LoadAvatar(charFileNoExt);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid()) return 0;

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            var tr = FindCharacter(root.transform, keywords);
            if (tr == null) continue;

            var animator = tr.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(tr.gameObject);
            Undo.RecordObject(animator, "Wire Enemy Animator");
            animator.runtimeAnimatorController = ctrl;
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            count++;
        }
        return count;
    }

    // Busca el GameObject del personaje: nombre que contiene alguna keyword y
    // que tenga un SkinnedMeshRenderer (asi no confundimos con un hueso suelto).
    private static Transform FindCharacter(Transform root, string[] keywords)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            string n = tr.name.ToLowerInvariant();
            bool match = false;
            for (int i = 0; i < keywords.Length; i++)
                if (n.Contains(keywords[i])) { match = true; break; }
            if (!match) continue;
            if (tr.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
            return tr;
        }
        return null;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(CTRL_DIR))
            AssetDatabase.CreateFolder(ENEMY_DIR, "Controllers");
    }
}
#endif
