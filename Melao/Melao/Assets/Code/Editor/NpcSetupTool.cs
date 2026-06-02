#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Crea los prefabs de los NPC prisioneros (lideres rescatables) a partir de sus
// rigs en Assets/Animacion/NPC, con su controller (Idle / JoyfulJump / Running),
// el avatar del rig, los materiales del modelo texturizado y una capsula. Ademas
// añade el emote JoyfulJump al Animator de Pops.
//
// Mapeo (GDD):
//   Cremi    -> lider de Chocolala  (Nivel 4)
//   Tathi    -> lider de Mas-melito (Nivel 2)
//   Trigorio -> lider de Boronitas  (Nivel 3)
//
// Menu: Tools > Melao > Setup NPCs (prisioneros)
public static class NpcSetupTool
{
    private const string NPC_DIR    = "Assets/Animacion/NPC";
    private const string CTRL_DIR   = "Assets/Animacion/NPC/Controllers";
    private const string CHAR_DIR   = "Assets/Personajes";
    private const string PREFAB_DIR = "Assets/Personajes/NPC";
    private const string POPS_CTRL  = "Assets/Animacion/PlayerAnimator.controller";
    private const string POPS_JOYFUL = "Assets/Animacion/JoyfulJumpPOPS.fbx";

    private struct NpcDef
    {
        public string rig;        // FBX del rig en Animacion/NPC
        public string model;      // FBX texturizado en Personajes (materiales)
        public string shortName;
        public string level;      // nivel donde aparece (informativo)
        public (string fbx, bool loop)[] clips; // Idle, JoyfulJump, Running
    }

    private static NpcDef[] Defs() => new[]
    {
        new NpcDef { rig="CremiRIG",     model="Cremi_texturizada",   shortName="Cremi",    level="Chocolala (Nivel 4)",
            clips=new (string,bool)[]{ ("IdleCREMI",true), ("JoyfulJumpCREMI",false), ("RunningCREMI",true) } },
        new NpcDef { rig="TathiRIG",     model="Tathi_texturizada",   shortName="Tathi",    level="Mas-melito (Nivel 2)",
            clips=new (string,bool)[]{ ("IdleTHATI",true), ("JoyfulJumpTHATI",false), ("RunningTHATI",true) } },
        new NpcDef { rig="TrigorioRIG",  model="Trigorio_texturizado",shortName="Trigorio", level="Boronitas (Nivel 3)",
            clips=new (string,bool)[]{ ("IdleTRIGORIO",true), ("JoyfulJumpTRIGORIO",false), ("RunningTRIGORIO",true) } },
    };

    [MenuItem("Tools/Melao/Setup NPCs (prisioneros)")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(CTRL_DIR)) AssetDatabase.CreateFolder(NPC_DIR, "Controllers");
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR)) AssetDatabase.CreateFolder(CHAR_DIR, "NPC");

        var made = new System.Text.StringBuilder();
        foreach (var d in Defs())
        {
            BindClips(d);                            // avatar + loop + bake root
            var ctrl = BuildController(d);
            if (CreatePrefab(d, ctrl)) made.Append($"  {d.shortName}  ->  {d.level}\n");
            else made.Append($"  (falta rig) {d.rig}\n");
        }

        bool popsOk = AddPopsJoyfulJump();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"NPC prisioneros creados en {PREFAB_DIR}:\n\n{made}\n" +
                     (popsOk ? "Emote JoyfulJump añadido al Animator de Pops.\n"
                             : "AVISO: no se pudo añadir JoyfulJump a Pops (revisa PlayerAnimator/clip).\n") +
                     "\nArrastra cada NPC a su nivel (atrapado) y añade un RescueCutscene\n" +
                     "(trigger) con el NPC + puntos asignados para el rescate.";
        Debug.Log("[NpcSetup] Listo.\n" + msg);
        EditorUtility.DisplayDialog("Setup NPCs", msg, "OK");
    }

    // -------- clips: vincular al avatar del rig + loop + bake root --------
    private static void BindClips(NpcDef d)
    {
        Avatar rigAvatar = EnsureRigAvatar(d.rig);
        foreach (var c in d.clips)
            SetClip(c.fbx, c.loop, rigAvatar);
    }

    private static Avatar EnsureRigAvatar(string fbxNoExt)
    {
        string path = $"{NPC_DIR}/{fbxNoExt}.fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"[NpcSetup] No importer en {path}"); return null; }
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importCameras = false;
        mi.importLights = false;
        EditorUtility.SetDirty(mi);
        mi.SaveAndReimport();
        return LoadAvatar(path);
    }

    private static void SetClip(string fbxNoExt, bool loop, Avatar copyAvatar)
    {
        string path = $"{NPC_DIR}/{fbxNoExt}.fbx";
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"[NpcSetup] No importer en {path}"); return; }

        mi.animationType = ModelImporterAnimationType.Generic;
        if (copyAvatar != null)
        {
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
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
        EditorUtility.SetDirty(mi);
        mi.SaveAndReimport();
    }

    // -------- controller: Idle <-> Running (bool), JoyfulJump one-shot --------
    private static AnimatorController BuildController(NpcDef d)
    {
        string path = $"{CTRL_DIR}/{d.shortName}_NPC.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        else ClearController(ctrl);

        ctrl.AddParameter("Running", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("JoyfulJump", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = AddState(sm, "Idle", Clip(d.clips[0].fbx));
        var run  = AddState(sm, "Running", Clip(d.clips[2].fbx));
        sm.defaultState = idle;
        Bool(idle, run, "Running", true);
        Bool(run, idle, "Running", false);

        var joy = AddState(sm, "JoyfulJump", Clip(d.clips[1].fbx));
        OneShot(sm, joy, idle, "JoyfulJump");
        return ctrl;
    }

    private static bool CreatePrefab(NpcDef d, AnimatorController ctrl)
    {
        string rigPath = $"{NPC_DIR}/{d.rig}.fbx";
        var rigModel = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
        if (rigModel == null) { Debug.LogWarning($"[NpcSetup] No se encontro {rigPath}"); return false; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(rigModel);
        if (inst == null) return false;

        var animator = inst.GetComponentInChildren<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        var av = LoadAvatar(rigPath);
        if (av != null) animator.avatar = av;
        animator.applyRootMotion = false;

        if (inst.GetComponent<Collider>() == null) ColliderFitter.FitCapsule(inst);
        if (inst.GetComponent<NpcCharacter>() == null) inst.AddComponent<NpcCharacter>();

        CopyMaterials(inst, $"{CHAR_DIR}/{d.model}.fbx");

        string prefabPath = $"{PREFAB_DIR}/{d.shortName}_NPC.prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        return true;
    }

    // -------- emote JoyfulJump en el Animator de Pops --------
    private static bool AddPopsJoyfulJump()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(POPS_CTRL);
        if (ctrl == null) { Debug.LogWarning($"[NpcSetup] No se encontro {POPS_CTRL}"); return false; }

        var clip = ClipAt(POPS_JOYFUL);
        if (clip == null) { Debug.LogWarning($"[NpcSetup] No se encontro clip en {POPS_JOYFUL}"); return false; }

        bool hasParam = false;
        foreach (var p in ctrl.parameters) if (p.name == "JoyfulJump") { hasParam = true; break; }
        if (!hasParam) ctrl.AddParameter("JoyfulJump", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        AnimatorState joy = null;
        foreach (var s in sm.states) if (s.state.name == "JoyfulJump") { joy = s.state; break; }
        if (joy == null)
        {
            joy = sm.AddState("JoyfulJump");
            joy.motion = clip;
            joy.writeDefaultValues = true;
            var back = sm.defaultState != null ? sm.defaultState : joy;
            OneShot(sm, joy, back, "JoyfulJump");
        }
        else joy.motion = clip;

        EditorUtility.SetDirty(ctrl);
        return true;
    }

    // ---------------- helpers ----------------
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
        if (clip == null) Debug.LogWarning($"[NpcSetup] Clip nulo para '{name}'.");
        return st;
    }

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
            outT.duration = 0.08f;
        }
    }

    private static void Bool(AnimatorState src, AnimatorState dst, string param, bool value)
    {
        var t = src.AddTransition(dst);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    private static AnimationClip Clip(string fbxNoExt) => ClipAt($"{NPC_DIR}/{fbxNoExt}.fbx");

    private static AnimationClip ClipAt(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    private static Avatar LoadAvatar(string fbxPath)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is Avatar a) return a;
        return null;
    }

    private static void CopyMaterials(GameObject go, string texturedFbxPath)
    {
        var list = new List<Material>();
        var mi = AssetImporter.GetAtPath(texturedFbxPath) as ModelImporter;
        if (mi != null)
            foreach (var kv in mi.GetExternalObjectMap())
                if (kv.Value is Material m && !list.Contains(m)) list.Add(m);
        if (list.Count == 0)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(texturedFbxPath);
            if (src != null)
                foreach (var r in src.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !list.Contains(m)) list.Add(m);
        }
        if (list.Count == 0) { Debug.LogWarning($"[NpcSetup] Sin materiales en {texturedFbxPath}"); return; }

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            int sub = 1;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) sub = smr.sharedMesh.subMeshCount;
            sub = Mathf.Max(1, sub);
            var mats = new Material[sub];
            for (int i = 0; i < sub; i++) mats[i] = list[Mathf.Min(i, list.Count - 1)];
            r.sharedMaterials = mats;
        }
    }
}
#endif
