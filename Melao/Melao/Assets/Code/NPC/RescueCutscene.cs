using System.Collections;
using UnityEngine;

// Cutscene de RESCATE del prisionero del nivel. Al entrar Pops en el trigger:
//   1) Toma el control (congela a Pops y desactiva su input).
//   2) Pops y el NPC hacen el emote JOYFUL JUMP (celebracion del rescate).
//   3) Se quedan un momento "hablando" (Idle).
//   4) Se van CORRIENDO hacia el punto de salida para continuar.
//   5) Devuelve el control a Pops (y opcionalmente dispara el fin de nivel).
//
// Es scripted (no usa Timeline; el proyecto no lo tiene). Coloca este componente
// en un GameObject con un Collider TRIGGER en la zona del prisionero, asigna el
// NPC y los puntos. La herramienta Tools > Melao > Setup NPCs deja un ejemplo.
[RequireComponent(typeof(Collider))]
public class RescueCutscene : MonoBehaviour
{
    [Header("Actores")]
    [Tooltip("El prisionero (prefab de NpcSetupTool).")]
    [SerializeField] private NpcCharacter npc;

    [Header("Puntos")]
    [Tooltip("Opcional: punto X donde Pops se planta frente al NPC. Si es null, usa su posicion actual.")]
    [SerializeField] private Transform popsStandPoint;
    [Tooltip("Hacia donde corren Pops y el NPC al terminar (para 'continuar').")]
    [SerializeField] private Transform runExitPoint;

    [Header("Tiempos (segundos)")]
    [SerializeField] private float joyfulJumpTime = 1.6f;
    [SerializeField] private float talkTime = 1.5f;
    [SerializeField] private float runTime = 2.2f;
    [SerializeField] private float runSpeed = 4.5f;

    [Header("Opciones")]
    [Tooltip("Solo se reproduce una vez.")]
    [SerializeField] private bool oneShot = true;
    [Tooltip("Al terminar, devuelve el control a Pops.")]
    [SerializeField] private bool returnControl = true;

    [Header("Avanzar de nivel al terminar (rescate = fin de nivel)")]
    [Tooltip("Si esta activo, al terminar la cutscene hace la transicion al siguiente nivel (o a creditos si 'nextLevelName' esta vacio).")]
    [SerializeField] private bool advanceLevelOnEnd = true;
    [Tooltip("Nombre de la escena del siguiente nivel. Vacio = fin del juego (creditos).")]
    [SerializeField] private string nextLevelName;
    [Tooltip("Nombre de esta escena (para marcar completado). Vacio = se autodetecta.")]
    [SerializeField] private string currentLevelName;

    private bool played;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (played && oneShot) return;
        if (!other.transform.root.CompareTag("Player")) return;
        played = true;
        StartCoroutine(Play(other.transform.root.gameObject));
    }

    private IEnumerator Play(GameObject player)
    {
        var control = player.GetComponent<PlayerController2_5D>();
        var rb = player.GetComponent<Rigidbody>();
        var popsAnim = player.GetComponentInChildren<Animator>();

        // 1) Tomar control: congelar a Pops.
        bool prevKinematic = false;
        if (control != null) control.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            prevKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }
        if (popsStandPoint != null)
        {
            Vector3 p = player.transform.position;
            p.x = popsStandPoint.position.x;
            player.transform.position = p;
        }

        // NPC y Pops se miran.
        if (npc != null) npc.FaceTowards(player.transform.position);

        // 2) Joyful jump (ambos).
        SafeTrigger(popsAnim, "JoyfulJump");
        if (npc != null) npc.PlayJoyfulJump();
        yield return new WaitForSeconds(joyfulJumpTime);

        // 3) Hablar (se quedan parados en Idle).
        yield return new WaitForSeconds(talkTime);

        // 4) Se van corriendo hacia la salida.
        if (npc != null) npc.SetRunning(true);
        SafeBool(popsAnim, "Moving", true);

        if (runExitPoint != null)
        {
            float t = 0f;
            int dir = runExitPoint.position.x >= player.transform.position.x ? 1 : -1;
            if (npc != null) npc.FaceTowards(runExitPoint.position);
            while (t < runTime)
            {
                t += Time.deltaTime;
                Vector3 step = Vector3.right * dir * runSpeed * Time.deltaTime;
                player.transform.position += step;
                if (npc != null) npc.transform.position += step;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(runTime);
        }

        // 5) Fin: devolver control y/o avanzar de nivel.
        SafeBool(popsAnim, "Moving", false);
        if (npc != null) npc.SetRunning(false);
        if (rb != null) rb.isKinematic = prevKinematic;
        if (returnControl && control != null) control.enabled = true;

        if (advanceLevelOnEnd)
        {
            // El rescate ES el fin del nivel: transicion al siguiente (o creditos).
            string cur = string.IsNullOrEmpty(currentLevelName) ? gameObject.scene.name : currentLevelName;
            ScreenFader.GetOrCreate().FadeToLevel(nextLevelName, cur);
        }
    }

    private static void SafeTrigger(Animator a, string name)
    {
        if (a == null) return;
        foreach (var p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) { a.SetTrigger(name); return; }
    }

    private static void SafeBool(Animator a, string name, bool value)
    {
        if (a == null) return;
        foreach (var p in a.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool) { a.SetBool(name, value); return; }
    }
}
