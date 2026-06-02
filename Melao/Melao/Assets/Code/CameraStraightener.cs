using UnityEngine;
using UnityEngine.SceneManagement;

// Nivela la camara para que el mapa NO se vea diagonal: elimina el "roll" (giro
// en Z) del horizonte. Opcionalmente tambien el "pitch" (X) para una vista
// lateral totalmente recta. NO mueve ningun objeto del nivel; solo corrige la
// rotacion de la camara.
//
// Se AUTO-INSTALA en la camara principal de TODAS las escenas al cargar (via
// RuntimeInitializeOnLoadMethod + sceneLoaded), asi funciona en todos los
// niveles sin tener que tocar cada escena. Corre tarde (DefaultExecutionOrder
// alto) para imponerse incluso sobre Cinemachine.
[DefaultExecutionOrder(10000)]
public class CameraStraightener : MonoBehaviour
{
    [Tooltip("Eliminar el giro en Z (lo que hace ver el mapa 'diagonal'). Recomendado ON.")]
    public bool zeroRoll = true;

    [Tooltip("Eliminar tambien la inclinacion vertical (X) para una vista lateral recta.")]
    public bool zeroPitch = false;

    private void LateUpdate()
    {
        Vector3 e = transform.eulerAngles;
        if (zeroRoll) e.z = 0f;
        if (zeroPitch) e.x = 0f;
        transform.eulerAngles = e;
    }

    // ----------------------------------------------------------------
    //   AUTO-INSTALACION EN TODAS LAS ESCENAS
    // ----------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureOnMainCamera();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureOnMainCamera();
    }

    private static void EnsureOnMainCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (cam.GetComponent<CameraStraightener>() == null)
            cam.gameObject.AddComponent<CameraStraightener>();
    }
}
