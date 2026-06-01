using UnityEngine;
using UnityEngine.InputSystem;

public class PowerUpController : MonoBehaviour
{
    [Header("Power Up Sprites (orden = indice)")]
    [SerializeField] private Sprite[] powerUpSprites;

    [Header("Input (se auto-asigna si se deja vacío)")]
    [SerializeField] private PlayerControls playerControls;

    private int currentIndex = -1;

    private void Awake()
    {
        if (playerControls == null)
            playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        playerControls.Player.UsePowerUp.performed += OnUsePowerUp;
        playerControls.Player.NextPowerUp.performed += OnNextPowerUp;
        playerControls.Player.PrevPowerUp.performed += OnPrevPowerUp;
        playerControls.Player.Enable();
    }

    private void OnDisable()
    {
        playerControls.Player.UsePowerUp.performed -= OnUsePowerUp;
        playerControls.Player.NextPowerUp.performed -= OnNextPowerUp;
        playerControls.Player.PrevPowerUp.performed -= OnPrevPowerUp;
    }

    private void OnUsePowerUp(InputAction.CallbackContext ctx)
    {
        UseCurrent();
    }

    private void OnNextPowerUp(InputAction.CallbackContext ctx)
    {
        if (powerUpSprites == null || powerUpSprites.Length == 0) return;
        if (currentIndex < 0) currentIndex = 0;
        currentIndex = (currentIndex + 1) % powerUpSprites.Length;
        UpdateHUD();
    }

    private void OnPrevPowerUp(InputAction.CallbackContext ctx)
    {
        if (powerUpSprites == null || powerUpSprites.Length == 0) return;
        if (currentIndex < 0) currentIndex = 0;
        currentIndex = (currentIndex - 1 + powerUpSprites.Length) % powerUpSprites.Length;
        UpdateHUD();
    }

    public void EquipPowerUp(int index)
    {
        currentIndex = index;
        UpdateHUD();
    }

    public void ClearPowerUp()
    {
        currentIndex = -1;
        UpdateHUD();
    }

    private void UseCurrent()
    {
        if (currentIndex < 0) return;
    }

    private void UpdateHUD()
    {
        if (currentIndex >= 0 && powerUpSprites != null && currentIndex < powerUpSprites.Length)
            HUDController.Instance?.UpdatePowerUp(powerUpSprites[currentIndex]);
        else
            HUDController.Instance?.UpdatePowerUp(null);
    }
}
