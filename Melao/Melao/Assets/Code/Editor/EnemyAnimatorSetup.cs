#if UNITY_EDITOR
using System.Collections.Generic;
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
//   DonPerico  (Idle, RunCycle, shoot)             -> Don_Perico_texturizado
//   Rita       (idle, Walkcycle, Splash)           -> Rita_conTexturas
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
        public string model;       // .fbx TEXTURIZADO en Personajes (para copiar materiales)
        public string rig;         // .fbx RIG en Enemigos (base con esqueleto que SÍ anima)
        public string shortName;   // nombre corto para prefab/controller
        public System.Func<AnimatorController> build;
        public string[] keywords;  // para localizarlo en escena
        public System.Type behavior; // script de comportamiento (BononEnemy, etc.)
    }

    // El prefab se construye sobre el RIG (las animaciones se hicieron sobre el,
    // asi que SÍ deforma) y se le copian los materiales del modelo texturizado.
    private static EnemyDef[] Defs()
    {
        return new[]
        {
            new EnemyDef { model = "Bonon_texturizado",       rig = "rig_bonon",   shortName = "Bonon",      build = BuildBonon,     keywords = new[]{ "bonon" },                          behavior = typeof(BononEnemy) },
            new EnemyDef { model = "Sir_Saladin_Texturizado", rig = "rig_SSS",     shortName = "SirSaladin", build = BuildSaladin,   keywords = new[]{ "saladin", "sir", "sss" },          behavior = typeof(SaladinBoss) },
            new EnemyDef { model = "Yucat_texturizado",       rig = "rig_Yucat",   shortName = "Yucat",      build = BuildYucat,     keywords = new[]{ "yucat" },                          behavior = typeof(YucatEnemy) },
            new EnemyDef { model = "Nuelito_texturizadp",     rig = "rig_Ñuelito", shortName = "Nuelito",    build = BuildNuelito,   keywords = new[]{ "nuelito", "ñuelito" },             behavior = typeof(NuelitoEnemy) },
            new EnemyDef { model = "Don_Perico_texturizado",  rig = "rig_DonP",    shortName = "DonPerico",  build = BuildDonPerico, keywords = new[]{ "perico", "donp", "don_perico" },    behavior = typeof(DonPericoEnemy) },
            new EnemyDef { model = "Rita_conTexturas",        rig = "rig_Rita",    shortName = "Rita",       build = BuildRita,      keywords = new[]{ "rita" },                           behavior = typeof(RitaEnemy) },
        };
    }

    // Construye los prefabs (rig + textura + comportamiento) y REEMPLAZA en la
    // escena abierta los personajes texturizados por instancias del prefab
    // (mismo lugar). Asi los enemigos de la escena SÍ animan.
    [MenuItem("Tools/Melao/Setup Enemy Animators")]
    public static void Run()
    {
        EnsureFolder();
        PrepareLoops();
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
            AssetDatabase.CreateFolder(CHAR_DIR, "Enemigos");

        foreach (var d in Defs()) CreatePrefab(d.build(), d);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Enemy Animators");
        int undo = Undo.GetCurrentGroup();

        int n = 0;
        foreach (var d in Defs())
            n += ReplaceInScene(d);

        Undo.CollapseUndoOperations(undo);

        Debug.Log($"[EnemyAnim] Prefabs listos. Enemigos reemplazados en escena: {n}.");
        EditorUtility.DisplayDialog("Setup Enemy Animators",
            $"Prefabs (rig animado + textura + comportamiento) en {PREFAB_DIR}.\n\n" +
            $"Enemigos reemplazados en la escena: {n}\n\n" +
            "Guarda la escena (Ctrl+S). Re-ejecutar no duplica (omite los ya configurados).",
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
            if (CreatePrefab(ctrl, d))
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
        // FIX DEFINITIVO de animaciones Generic: cada clip debe estar VINCULADO
        // al avatar del modelo (rig) sobre el que se reproduce. Si no, la clip
        // corre pero no deforma la malla (lo que pasaba con Yucat/Ñuelito).
        //   - El modelo fuente (rig) -> Avatar 'Create From This Model'.
        //   - Cada clip de ese enemigo -> 'Copy From Other Avatar' = avatar del rig.
        // Ademas: loop en ciclos + Bake Into Pose del root (anti-teletransporte).

        foreach (var e in EnemyClipData())
            BindEnemyClips(e.avatarSource, e.clips);

        AssetDatabase.Refresh();
    }

    // Datos compartidos: por enemigo, el FBX fuente del avatar (modelo con malla)
    // y la lista de (clipFbx, loop). Usado por el setup y por el diagnostico.
    private static (string shortName, string avatarSource, (string fbx, bool loop)[] clips)[] EnemyClipData()
    {
        return new (string, string, (string, bool)[])[]
        {
            ("Bonon", "rig_bonon", new (string, bool)[] {
                ("Bonon_ANI_Walk Cycle", true), ("Bonon_ANI_Dash", false), ("Bonon_ANI_Jump", false) }),
            ("Yucat", "rig_Yucat", new (string, bool)[] {
                ("Yucat_ANI_Idle", true), ("Yucat_AniWalkCycle", true), ("Yucat_ANI_Ataque", false) }),
            ("SirSaladin", "rig_SSS", new (string, bool)[] {
                ("SSS_Ani_idle", true), ("SSS_Ani_Run cycle", true), ("SSS_Ani_Warning", false), ("SSS_Ani_Crash", false) }),
            ("Nuelito", "rig_Ñuelito", new (string, bool)[] {
                ("Ñuelito_ANIWalk Cycle", true), ("Ñuelito_ANIShooting", false) }),
            ("DonPerico", "rig_DonP", new (string, bool)[] {
                ("DonP_ani_Idle", true), ("DonP_ani_RunCycle", true), ("DonP_ani_shoot", false) }),
            ("Rita", "rig_Rita", new (string, bool)[] {
                ("rita_ani_idle", true), ("rita_ani_Walkcycle", true), ("rita_ani_Splash", false) }),
        };
    }

    // Verifica que las clips de cada enemigo deforman su modelo: compara las
    // rutas de las curvas de cada clip con la jerarquia del rig. Si el % es bajo,
    // la animacion NO se vera. Reporta tambien clips nulos y loop.
    [MenuItem("Tools/Melao/Diagnose Enemy Animations")]
    public static void Diagnose()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in EnemyClipData())
        {
            sb.AppendLine($"== {e.shortName}  (modelo: {e.avatarSource}) ==");
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>($"{ENEMY_DIR}/{e.avatarSource}.fbx");
            if (rig == null) { sb.AppendLine("   MODELO NO ENCONTRADO"); continue; }

            var paths = new HashSet<string>();
            CollectPaths(rig.transform, rig.transform, paths);

            foreach (var c in e.clips)
            {
                var clip = Clip(c.fbx);
                if (clip == null) { sb.AppendLine($"   {c.fbx}: CLIP NULL (no carga)"); continue; }

                var bindings = AnimationUtility.GetCurveBindings(clip);
                var seen = new HashSet<string>();
                int total = 0, matched = 0;
                foreach (var b in bindings)
                {
                    if (!seen.Add(b.path)) continue;
                    total++;
                    if (paths.Contains(b.path)) matched++;
                }
                string verdict = (total == 0) ? "sin curvas"
                               : (matched == total) ? "OK"
                               : (matched == 0) ? "NO COINCIDE (no anima)"
                               : "PARCIAL";
                sb.AppendLine($"   {c.fbx}: {matched}/{total} rutas, loop={clip.isLooping} -> {verdict}");
            }
        }
        string report = sb.ToString();
        Debug.Log("[EnemyAnim DIAG]\n" + report);
        EditorUtility.DisplayDialog("Diagnose Enemy Animations", report, "OK");
    }

    private static void CollectPaths(Transform root, Transform t, HashSet<string> paths)
    {
        paths.Add(AnimationUtility.CalculateTransformPath(t, root));
        for (int i = 0; i < t.childCount; i++) CollectPaths(root, t.GetChild(i), paths);
    }

    // Asegura el avatar del modelo fuente y vincula todas las clips a el.
    private static void BindEnemyClips(string avatarSourceFbx, (string fbx, bool loop)[] clips)
    {
        Avatar rigAvatar = EnsureRigAvatar(avatarSourceFbx);
        if (rigAvatar == null)
            Debug.LogWarning($"[EnemyAnim] No se pudo generar avatar de {avatarSourceFbx}. Las clips podrian no deformar.");

        foreach (var c in clips)
            SetClip(c.fbx, c.loop, c.fbx == avatarSourceFbx ? null : rigAvatar);
    }

    // Genera/asegura el avatar del FBX fuente (Create From This Model) y lo devuelve.
    private static Avatar EnsureRigAvatar(string fbxNoExt)
    {
        string path = $"{ENEMY_DIR}/{fbxNoExt}.fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"[EnemyAnim] No importer en {path}"); return null; }
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // Algunos FBX de rig traen camaras/luces de la escena del artista. No las
        // queremos en el prefab del enemigo: desactivar su importacion.
        mi.importCameras = false;
        mi.importLights = false;
        EditorUtility.SetDirty(mi);
        mi.SaveAndReimport();
        return LoadAvatarAtPath(path);
    }

    private static bool CreatePrefab(AnimatorController ctrl, EnemyDef d)
    {
        // Base = RIG (anima de verdad porque las clips se hicieron sobre el).
        string rigPath = $"{ENEMY_DIR}/{d.rig}.fbx";
        var rigModel = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
        if (rigModel == null) { Debug.LogWarning($"[EnemyAnim] No se encontro rig {rigPath}"); return false; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(rigModel);
        if (inst == null) { Debug.LogWarning($"[EnemyAnim] No se pudo instanciar {rigPath}"); return false; }

        // Avatar del propio rig (coincide con la jerarquia de las clips).
        ConfigureEnemy(inst, ctrl, $"{ENEMY_DIR}/{d.rig}.fbx", d.behavior);
        // Texturas: copiar materiales del modelo texturizado por nombre de malla.
        CopyMaterials(inst, $"{CHAR_DIR}/{d.model}.fbx");

        string prefabPath = $"{PREFAB_DIR}/{d.shortName}_Enemy.prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        return true;
    }

    // Configura un GameObject de enemigo: Animator (controller+avatar), collider
    // de cuerpo (capsula), Rigidbody kinematico y el script de comportamiento.
    private static void ConfigureEnemy(GameObject go, AnimatorController ctrl, string avatarFbxPath, System.Type behavior)
    {
        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        var av = LoadAvatarAtPath(avatarFbxPath);
        if (av != null) animator.avatar = av;
        animator.applyRootMotion = false;

        EnsureBodyCollider(go);

        if (behavior != null && go.GetComponent(behavior) == null)
            go.AddComponent(behavior); // [RequireComponent(Rigidbody)] añade el RB

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // Aplica los MATERIALES REALES del modelo texturizado (los .mat externos que
    // referencia su importer) a todos los renderers del rig. Asi el enemigo
    // animado queda con la textura de Personajes. Cada renderer recibe un array
    // del largo correcto (= submeshes) -> ninguna malla queda invisible.
    private static void CopyMaterials(GameObject go, string texturedFbxPath)
    {
        Material[] mats = GetTexturizadoMaterials(texturedFbxPath);
        if (mats == null || mats.Length == 0)
        {
            Debug.LogWarning($"[EnemyAnim] No se hallaron materiales en {texturedFbxPath}; el enemigo conserva el material del rig.");
            return;
        }

        var dst = go.GetComponentsInChildren<Renderer>(true);
        foreach (var d in dst)
        {
            int subCount = Mathf.Max(1, SubmeshCount(d));
            var newMats = new Material[subCount];
            for (int i = 0; i < subCount; i++)
                newMats[i] = mats[Mathf.Min(i, mats.Length - 1)];
            d.sharedMaterials = newMats;
        }
    }

    // Obtiene los .mat reales del texturizado: primero del mapa de objetos
    // externos del importer (lo mas fiable), luego de sus renderers.
    private static Material[] GetTexturizadoMaterials(string texturedFbxPath)
    {
        var list = new List<Material>();

        var mi = AssetImporter.GetAtPath(texturedFbxPath) as ModelImporter;
        if (mi != null)
        {
            foreach (var kv in mi.GetExternalObjectMap())
                if (kv.Value is Material m && !list.Contains(m)) list.Add(m);
        }

        if (list.Count == 0)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(texturedFbxPath);
            if (go != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !list.Contains(m)) list.Add(m);
        }
        return list.ToArray();
    }

    private static int SubmeshCount(Renderer r)
    {
        if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) return smr.sharedMesh.subMeshCount;
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.subMeshCount;
        return (r.sharedMaterials != null) ? Mathf.Max(1, r.sharedMaterials.Length) : 1;
    }

    // Añade y ajusta una CapsuleCollider de cuerpo usando los bounds COMBINADOS
    // de todas las mallas (via ColliderFitter), respetando la escala. Asi el
    // collider envuelve bien al personaje y no se hunde/flota.
    private static void EnsureBodyCollider(GameObject go)
    {
        if (go.GetComponent<Collider>() != null) return;
        ColliderFitter.FitCapsule(go);
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
        dash.speed = 1.6f; // un poco mas rapido, pero la clip se reproduce COMPLETA
        var jump = AddState(sm, "Jump", Clip("Bonon_ANI_Jump"));
        OneShot(sm, dash, walk, "Dash"); // clip completa (dive + levantarse) -> se ve bien
        OneShot(sm, jump, walk, "Jump");
        return ctrl;
    }

    // Sir Saladin (SSS): Idle <-> Run (Moving), Warning y Crash one-shot (vuelven a Idle).
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
        OneShot(sm, crash, idle, "Crash"); // tras chocar, vuelve a Idle
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
        var walk = AddState(sm, "Walk", Clip("Yucat_AniWalkCycle"));
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

    // Don Perico: Idle <-> Run (Moving), Shoot one-shot (vuelve a Idle).
    private static AnimatorController BuildDonPerico()
    {
        var ctrl = NewController("DonPerico");
        ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = AddState(sm, "Idle", Clip("DonP_ani_Idle"));
        var run  = AddState(sm, "Run",  Clip("DonP_ani_RunCycle"));
        sm.defaultState = idle;
        Bool(idle, run, "Moving", true);
        Bool(run, idle, "Moving", false);

        var shoot = AddState(sm, "Shoot", Clip("DonP_ani_shoot"));
        OneShot(sm, shoot, idle, "Shoot");
        return ctrl;
    }

    // Rita la frita: Idle <-> Walk (Moving), Splash one-shot (vuelve a Idle).
    private static AnimatorController BuildRita()
    {
        var ctrl = NewController("Rita");
        ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Splash", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = AddState(sm, "Idle", Clip("rita_ani_idle"));
        var walk = AddState(sm, "Walk", Clip("rita_ani_Walkcycle"));
        sm.defaultState = idle;
        Bool(idle, walk, "Moving", true);
        Bool(walk, idle, "Moving", false);

        var splash = AddState(sm, "Splash", Clip("rita_ani_Splash"));
        OneShot(sm, splash, idle, "Splash");
        return ctrl;
    }

    // -----------------------------------------------------------------
    //   HELPERS de construccion
    // -----------------------------------------------------------------
    // REUSA el controller existente (mismo GUID) y lo vacia para reconstruirlo.
    // Antes lo borraba+recreaba -> nuevo GUID -> las instancias en escena quedaban
    // con "Missing (Runtime Animator Controller)". Mantener el GUID arregla eso.
    private static AnimatorController NewController(string name)
    {
        string path = $"{CTRL_DIR}/{name}_Enemy.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null)
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        else
            ClearController(ctrl);
        return ctrl;
    }

    private static void ClearController(AnimatorController ctrl)
    {
        var ps = ctrl.parameters;
        for (int i = ps.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);

        if (ctrl.layers.Length > 0)
        {
            var sm = ctrl.layers[0].stateMachine;
            var anyTs = sm.anyStateTransitions;
            for (int i = anyTs.Length - 1; i >= 0; i--) sm.RemoveAnyStateTransition(anyTs[i]);
            var states = sm.states;
            for (int i = states.Length - 1; i >= 0; i--) sm.RemoveState(states[i].state);
        }
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

    // Igual que OneShot pero con exitTime configurable (vuelve antes a returnTo).
    private static void OneShotFast(AnimatorStateMachine sm, AnimatorState target, AnimatorState returnTo, string trigger, float exitTime)
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
            outT.exitTime = Mathf.Clamp01(exitTime);
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

    private static Avatar LoadAvatarAtPath(string fbxPath)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is Avatar av) return av;
        return null;
    }

    // Configura una clip: animationType Generic, vincula al avatar del rig (si
    // copyAvatar != null), pone loop + Bake Into Pose del root.
    private static void SetClip(string fileNoExt, bool loop, Avatar copyAvatar)
    {
        string path = $"{ENEMY_DIR}/{fileNoExt}.fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"[EnemyAnim] No importer en {path}"); return; }

        mi.animationType = ModelImporterAnimationType.Generic;
        if (copyAvatar != null)
        {
            // Vincular la clip al esqueleto del rig -> SÍ deforma la malla.
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = copyAvatar;
        }

        var clips = mi.clipAnimations;
        if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                // Bake Into Pose del Root: la clip anima en sitio; el script
                // controla el desplazamiento (anti-teletransporte).
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        EditorUtility.SetDirty(mi);
        mi.SaveAndReimport();
    }

    // -----------------------------------------------------------------
    //   REEMPLAZO EN ESCENA
    // -----------------------------------------------------------------
    // Reemplaza los personajes texturizados (que no animan) por instancias del
    // prefab rig-based (que sí anima), conservando posicion/rotacion/padre.
    // Omite objetos que ya tengan comportamiento de enemigo (no duplica).
    private static int ReplaceInScene(EnemyDef d)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/{d.shortName}_Enemy.prefab");
        if (prefab == null) return 0;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid()) return 0;

        // Recolectar matches primero (no modificar mientras se itera).
        var targets = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            CollectCharacters(root.transform, d.keywords, targets);

        int count = 0;
        foreach (var tr in targets)
        {
            if (tr == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(tr.parent, true);
            inst.transform.SetPositionAndRotation(tr.position, tr.rotation);
            inst.name = d.shortName + "_Enemy";
            Undo.RegisterCreatedObjectUndo(inst, "Spawn Enemy");
            Undo.DestroyObjectImmediate(tr.gameObject);
            count++;
        }
        return count;
    }

    // Junta todos los GameObjects con SkinnedMeshRenderer cuyo nombre contiene
    // una keyword y que NO sean ya un enemigo configurado (sin EnemyBase/SaladinBoss).
    private static void CollectCharacters(Transform root, string[] keywords, List<Transform> outList)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            string n = tr.name.ToLowerInvariant();
            bool match = false;
            for (int i = 0; i < keywords.Length; i++)
                if (n.Contains(keywords[i])) { match = true; break; }
            if (!match) continue;
            if (tr.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
            // Ya configurado (es un prefab nuestro) -> omitir.
            if (tr.GetComponent<EnemyBase>() != null || tr.GetComponent<SaladinBoss>() != null) continue;
            if (!outList.Contains(tr)) outList.Add(tr);
        }
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(CTRL_DIR))
            AssetDatabase.CreateFolder(ENEMY_DIR, "Controllers");
    }
}
#endif
