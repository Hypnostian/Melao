using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

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
    [Tooltip("Overlay radial que muestra el enfriamiento del power-up seleccionado.")]
    [SerializeField] private Image powerUpCooldown;
    [SerializeField] private TMP_Text powerUpNameText;
    [SerializeField] private Color emptySlotColor = new Color(0, 0, 0, 0.4f);
    [SerializeField] private Color filledSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);

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

    public void AddHeartSlot()
    {
        EnsureHeartsContainer();
        if (heartsContainer == null) return;

        GameObject heart;
        if (heartPrefab != null)
            heart = Instantiate(heartPrefab, heartsContainer);
        else
        {
            heart = new GameObject("Heart", typeof(Image));
            heart.transform.SetParent(heartsContainer, false);
            var rt = heart.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(24, 24);
        }

        Image img = heart.GetComponent<Image>();
        if (img == null) img = heart.AddComponent<Image>();
        if (heartImages.Count == 3 && heartExtra != null)
            img.sprite = heartExtra;
        heartImages.Add(img);
    }

    public void UpdatePowerUp(Sprite icon, bool hasPowerUp = false, PowerUpType type = PowerUpType.Cuquis)
    {
        if (powerUpIcon != null)
        {
            powerUpIcon.gameObject.SetActive(icon != null);
            if (icon != null) powerUpIcon.sprite = icon;
        }

        if (powerUpCooldown != null)
        {
            powerUpCooldown.sprite = icon;
            powerUpCooldown.gameObject.SetActive(false);
        }

        if (powerUpNameText != null)
        {
            powerUpNameText.gameObject.SetActive(icon == null && hasPowerUp);
            if (hasPowerUp) powerUpNameText.text = type.ToString();
        }

        var slotImg = powerUpEmptySlot?.GetComponent<Image>();
        if (slotImg == null) return;
        slotImg.color = icon != null || hasPowerUp ? filledSlotColor : emptySlotColor;
    }

    // fraction01: 1 = recien usado (cubierto), 0 = listo. La llama PowerUpController.
    public void UpdatePowerUpCooldown(float fraction01)
    {
        if (powerUpCooldown == null) return;
        bool show = fraction01 > 0.001f && powerUpCooldown.sprite != null;
        if (powerUpCooldown.gameObject.activeSelf != show)
            powerUpCooldown.gameObject.SetActive(show);
        if (show) powerUpCooldown.fillAmount = fraction01;
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

        // Overlay de enfriamiento: barrido radial oscuro sobre el icono.
        if (powerUpCooldown == null)
        {
            var coGo = new GameObject("PowerUpCooldown", typeof(RectTransform), typeof(Image));
            coGo.transform.SetParent(powerUpEmptySlot.transform, false);
            powerUpCooldown = coGo.GetComponent<Image>();
            powerUpCooldown.color = new Color(0f, 0f, 0f, 0.6f);
            powerUpCooldown.raycastTarget = false;
            powerUpCooldown.type = Image.Type.Filled;
            powerUpCooldown.fillMethod = Image.FillMethod.Radial360;
            powerUpCooldown.fillOrigin = (int)Image.Origin360.Top;
            powerUpCooldown.fillClockwise = false;
            powerUpCooldown.preserveAspect = true;

            var coRt = powerUpCooldown.rectTransform;
            coRt.anchorMin = new Vector2(0.5f, 0.5f);
            coRt.anchorMax = new Vector2(0.5f, 0.5f);
            coRt.pivot = new Vector2(0.5f, 0.5f);
            coRt.sizeDelta = new Vector2(40, 40);
            coGo.SetActive(false);
        }

        if (powerUpNameText == null)
        {
            var textGo = new GameObject("PowerUpName", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(powerUpEmptySlot.transform, false);
            powerUpNameText = textGo.GetComponent<TextMeshProUGUI>();
            powerUpNameText.fontSize = 10;
            powerUpNameText.alignment = TextAlignmentOptions.Center;
            powerUpNameText.color = Color.white;
            powerUpNameText.transform.SetAsLastSibling();

            var textRt = powerUpNameText.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
        }

        powerUpIcon.gameObject.SetActive(false);
        powerUpNameText.gameObject.SetActive(false);
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
