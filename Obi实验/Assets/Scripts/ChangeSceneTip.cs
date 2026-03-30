using UnityEngine;

public class ChangeSceneTip : MonoBehaviour
{
    public static ChangeSceneTip Instance;
    public GameObject changeScenePanel;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);
    }
    public void showChangeSceneTip()
    {
        changeScenePanel.gameObject.SetActive(true);
    }
    public void hideChangeSceneTip()
    {
        changeScenePanel.gameObject.SetActive(false);
    }
}
