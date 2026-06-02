using System.Collections.Generic;
using UnityEngine;

// NPC prisionero rescatable (Cremi, Tathi, Trigorio). La cutscene de rescate
// (RescueCutscene) controla sus animaciones. Tiene 3 estados de animacion:
//   Idle (atrapado / hablando), JoyfulJump (celebracion al ser salvado),
//   Running (se va corriendo para continuar).
//
// Helpers seguros: no fallan si al Animator le falta un parametro.
[DisallowMultipleComponent]
public class NpcCharacter : MonoBehaviour
{
    [Header("Plano 2.5D (igual que los enemigos)")]
    [Tooltip("Alinea el NPC al plano Z de Pops, para que quede en el MISMO plano de juego (no detras/delante).")]
    public bool lockToPlayerZ = true;
    [Tooltip("Mientras no esta corriendo, mira hacia la camara (look 2.5D, como Pops quieta).")]
    public bool faceCameraWhenIdle = true;
    [Tooltip("Offset de yaw si el modelo no mira a +Z por defecto.")]
    public float yawOffset = 0f;

    private Animator animator;
    private HashSet<string> animParams;
    private Transform player;
    private Transform cam;
    private bool running;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        animParams = new HashSet<string>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var p in animator.parameters) animParams.Add(p.name);
    }

    private void LateUpdate()
    {
        // Plano 2.5D: igual que EnemyBase, se alinea al Z del jugador.
        if (lockToPlayerZ)
        {
            if (player == null)
            {
                var pc = FindFirstObjectByType<PlayerController2_5D>();
                if (pc != null) player = pc.transform;
            }
            if (player != null)
            {
                Vector3 p = transform.position;
                p.z = player.position.z;
                transform.position = p;
            }
        }

        // Mirar a la camara cuando esta quieto (no mientras corre / la cutscene manda).
        if (faceCameraWhenIdle && !running)
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null)
            {
                Vector3 d = cam.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
            }
        }
    }

    public void PlayJoyfulJump() => SetTrigger("JoyfulJump");
    public void SetRunning(bool on) { running = on; SetBool("Running", on); }

    // Orienta al NPC hacia un punto (eje Y), por ejemplo para mirar a Pops o a la salida.
    public void FaceTowards(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    private void SetTrigger(string n) { if (animator != null && animParams.Contains(n)) animator.SetTrigger(n); }
    private void SetBool(string n, bool v) { if (animator != null && animParams.Contains(n)) animator.SetBool(n, v); }
}
