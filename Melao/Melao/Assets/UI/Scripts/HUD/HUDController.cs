using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("Corazones")]
    [SerializeField] private Transform heartsContainer;
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private Sprite heartFull;
    [SerializeField] private Sprite heartEmpty;
    [SerializeField] private Sprite heartExtra; // sprite especial del 4to corazón

    [Header("Power Up")]
    [SerializeField] private Image powerUpIcon;
    [SerializeField] private GameObject powerUpEmptySlot;

    // Lista de corazones instanciados en pantalla
    private List<Image> heartImages = new List<Image>();

    private void Awake()
    {
        Instance = this;
    }

    // Llamado por PlayerHealth al iniciar o cuando cambia maxHearts
    public void InitHearts(int maxHearts, int currentHearts)
    {
        // Limpiar corazones anteriores
        foreach (Transform child in heartsContainer)
            Destroy(child.gameObject);

        heartImages.Clear();

        // Instanciar los corazones necesarios
        for (int i = 0; i < maxHearts; i++)
        {
            GameObject heart = Instantiate(heartPrefab, heartsContainer);

            // El 4to corazón usa sprite especial si existe
            Image img = heart.GetComponent<Image>();
            if (i == 3 && heartExtra != null)
                img.sprite = heartExtra;

            heartImages.Add(img);
        }

        // Actualizar estado visual
        UpdateHearts(currentHearts, maxHearts);
        UpdatePowerUp(null);
    }

    // Actualizar qué corazones están llenos o vacíos
    public void UpdateHearts(int currentHearts, int maxHearts)
    {
        for (int i = 0; i < heartImages.Count; i++)
        {
            heartImages[i].sprite = i < currentHearts ? heartFull : heartEmpty;
        }
    }

    // Llamado cuando el jugador consigue el power up de corazón extra
    public void UnlockExtraHeart(int currentHearts)
    {
        // Redibujar con el nuevo máximo de 4
        InitHearts(4, currentHearts);
    }

    // Llamado cuando el jugador recoge o pierde un power up
    public void UpdatePowerUp(Sprite icon)
    {
        bool hasPowerUp = icon != null;
        powerUpIcon.gameObject.SetActive(hasPowerUp);
        powerUpEmptySlot.SetActive(!hasPowerUp);
        if (hasPowerUp) powerUpIcon.sprite = icon;
    }
}