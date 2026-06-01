using UnityEngine;

public class CreditsScreen : MonoBehaviour
{
    public void OnBackPressed()
    {
        UIManager.Instance.ShowScreen("MainMenu");
    }
}
