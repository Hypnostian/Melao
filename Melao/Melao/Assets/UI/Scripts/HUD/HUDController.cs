using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("Vida")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Text healthText;

    [Header("Power Up")]
    [SerializeField] private Image powerUpIcon;
    [SerializeField] private GameObject powerUpEmptySlot;

    [Header("Checkpoints")]
    [SerializeField] private Transform checkpointContainer;
    [SerializeField] private GameObject checkpointDotPrefab;

    private void Awake()
    {
        Instance = this;
    }

    // Llamado por el PlayerHealth cuando cambia la vida
    public void UpdateHealth(int current, int max)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = max;
            healthBar.value = current;
        }
        if (healthText != null)
            healthText.text = $"{current}/{max}";
    }

    // Llamado cuando el jugador recoge un power up
    public void UpdatePowerUp(Sprite icon)
    {
        bool hasPowerUp = icon != null;
        powerUpIcon.gameObject.SetActive(hasPowerUp);
        powerUpEmptySlot.SetActive(!hasPowerUp);
        if (hasPowerUp) powerUpIcon.sprite = icon;
    }

    // Genera visualmente los puntos de checkpoint al cargar el nivel
    public void InitCheckpoints(int count)
    {
        foreach (Transform child in checkpointContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < count; i++)
            Instantiate(checkpointDotPrefab, checkpointContainer);
    }

    // Activa visualmente el checkpoint N
    public void ActivateCheckpointDot(int index)
    {
        if (index < checkpointContainer.childCount)
        {
            Image dot = checkpointContainer
                .GetChild(index)
                .GetComponent<Image>();
            if (dot != null) dot.color = Color.yellow;
        }
    }
}