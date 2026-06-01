using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("Corazones")]
    [SerializeField] private Transform heartsContainer;
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private Sprite heartFull;
    [SerializeField] private Sprite heartEmpty;
    [SerializeField] private Sprite heartExtra;

    [Header("Power Up")]
    [SerializeField] private Image powerUpIcon;
    [SerializeField] private GameObject powerUpEmptySlot;

    private List<Image> heartImages = new List<Image>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        EnsurePowerUpUI();
    }

    public void InitHearts(int maxHearts, int currentHearts)
    {
        EnsureHeartsContainer();
        if (heartsContainer == null) return;

        var parentRt = transform.GetComponent<RectTransform>();
        parentRt.anchorMin = new Vector2(0, 1);
        parentRt.anchorMax = new Vector2(0, 1);
        parentRt.pivot = new Vector2(0, 1);
        parentRt.anchoredPosition = new Vector2(10, -10);

        var heartsRt = heartsContainer.GetComponent<RectTransform>();
        heartsRt.anchorMin = new Vector2(0, 1);
        heartsRt.anchorMax = new Vector2(0, 1);
        heartsRt.pivot = new Vector2(0, 1);
        heartsRt.anchoredPosition = Vector2.zero;

        foreach (Transform child in heartsContainer)
            Destroy(child.gameObject);

        heartImages.Clear();

        for (int i = 0; i < maxHearts; i++)
        {
            GameObject heart;
            if (heartPrefab != null)
            {
                heart = Instantiate(heartPrefab, heartsContainer);
            }
            else
            {
                heart = new GameObject("Heart", typeof(Image));
                heart.transform.SetParent(heartsContainer, false);
                var rt = heart.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(24, 24);
            }

            Image img = heart.GetComponent<Image>();
            if (img == null) img = heart.AddComponent<Image>();
            if (i == 3 && heartExtra != null)
                img.sprite = heartExtra;
            heartImages.Add(img);
        }

        UpdateHearts(currentHearts, maxHearts);
        UpdatePowerUp(null);
    }

    public void UpdateHearts(int currentHearts, int maxHearts)
    {
        for (int i = 0; i < heartImages.Count; i++)
        {
            if (heartImages[i] == null) continue;
            if (heartFull != null && heartEmpty != null)
                heartImages[i].sprite = i < currentHearts ? heartFull : heartEmpty;
            else
                heartImages[i].color = i < currentHearts ? Color.red : Color.gray;
        }
    }

    public void UnlockExtraHeart(int currentHearts)
    {
        InitHearts(4, currentHearts);
    }

    public void UpdatePowerUp(Sprite icon)
    {
        if (powerUpIcon != null)
            powerUpIcon.gameObject.SetActive(icon != null);
        if (powerUpEmptySlot != null)
            powerUpEmptySlot.SetActive(icon == null);
        if (icon != null && powerUpIcon != null)
            powerUpIcon.sprite = icon;
    }

    private void EnsurePowerUpUI()
    {
        // Crea el slot vacío si no está asignado
        if (powerUpEmptySlot == null)
        {
            powerUpEmptySlot = new GameObject("PowerUpSlot", typeof(RectTransform), typeof(Image));
            powerUpEmptySlot.transform.SetParent(transform, false);
            var img = powerUpEmptySlot.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0.4f);

            var rt = powerUpEmptySlot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(48, 48);
        }

        // Ancla el slot debajo de los corazones
        var slotRt = powerUpEmptySlot.GetComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0, 1);
        slotRt.anchorMax = new Vector2(0, 1);
        slotRt.pivot = new Vector2(0, 1);
        slotRt.anchoredPosition = new Vector2(10, -70);

        // Crea el icono dentro del slot si no está asignado
        if (powerUpIcon == null)
        {
            var iconGo = new GameObject("PowerUpIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(powerUpEmptySlot.transform, false);
            powerUpIcon = iconGo.GetComponent<Image>();
            powerUpIcon.preserveAspect = true;

            var iconRt = powerUpIcon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(40, 40);
        }

        powerUpEmptySlot.SetActive(false);
    }

    private void EnsureHeartsContainer()
    {
        if (heartsContainer != null) return;
        var found = transform.Find("Hearts");
        if (found != null)
        {
            heartsContainer = found;
            return;
        }
        var go = new GameObject("Hearts", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        heartsContainer = go.transform;
    }
}
