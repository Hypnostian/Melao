#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Ajusta una CapsuleCollider para que envuelva bien la forma del personaje,
// usando los bounds COMBINADOS de todos sus renderers (mallas) y respetando la
// escala del transform. Evita colliders mal dimensionados que causan que el
// personaje se hunda en el piso, flote o quede mal posicionado.
//
// Menu: Tools > Melao > Fit Capsule Collider To Selection
//   (selecciona uno o varios personajes y corre el menu)
//
// Tambien expone FitCapsule(GameObject) reutilizable por otras herramientas.
public static class ColliderFitter
{
    [MenuItem("Tools/Melao/Fit Capsule Collider To Selection")]
    public static void FitSelection()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            EditorUtility.DisplayDialog("Fit Capsule", "Selecciona al menos un GameObject (personaje).", "OK");
            return;
        }

        int n = 0;
        foreach (var go in sel)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Fit Capsule Collider");
            if (FitCapsule(go)) n++;
        }
        Debug.Log($"[ColliderFitter] Capsulas ajustadas: {n}");
    }

    // Crea (si falta) y ajusta una CapsuleCollider al volumen del personaje.
    // Devuelve true si pudo dimensionarla a partir de renderers.
    public static bool FitCapsule(GameObject go)
    {
        var cap = go.GetComponent<CapsuleCollider>();
        if (cap == null) cap = Undo.AddComponent<CapsuleCollider>(go);

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            cap.direction = 1;
            cap.height = 1f; cap.radius = 0.3f; cap.center = new Vector3(0f, 0.5f, 0f);
            return false;
        }

        // Bounds combinados en mundo.
        Bounds wb = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) wb.Encapsulate(renderers[i].bounds);

        // Centro en espacio local del root.
        Vector3 localCenter = go.transform.InverseTransformPoint(wb.center);

        // Tamano local = tamano mundo / escala (las dimensiones del collider se
        // multiplican por lossyScale en runtime).
        Vector3 s = go.transform.lossyScale;
        float sx = Mathf.Abs(s.x) < 1e-4f ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Abs(s.y) < 1e-4f ? 1f : Mathf.Abs(s.y);
        float sz = Mathf.Abs(s.z) < 1e-4f ? 1f : Mathf.Abs(s.z);
        Vector3 localSize = new Vector3(wb.size.x / sx, wb.size.y / sy, wb.size.z / sz);

        cap.direction = 1; // eje Y
        cap.center = localCenter;
        cap.height = Mathf.Max(0.2f, localSize.y);
        // radio: la mitad del lado horizontal mayor, con un leve margen interno.
        cap.radius = Mathf.Max(0.08f, Mathf.Max(localSize.x, localSize.z) * 0.42f);

        EditorUtility.SetDirty(cap);
        return true;
    }
}
#endif
