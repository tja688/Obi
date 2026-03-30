using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ChangeScenePanel : MonoBehaviour
{
    private int nowLevel;
    private TextMeshProUGUI[] texts;
    private Button[] buttons;
    public GameObject player;
    public static ChangeScenePanel Instance;
    private void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
            Destroy(Instance);
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateButton();
    }
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        UpdateButton();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    public void UpdateButton()
    {
        nowLevel = InteractElevator.Instance.nowLevel;
        player = GameObject.Find("player");
        texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        int i = 7;
        for (int j = 0; j < texts.Length; j++)
        {
            if (j == texts.Length - 1)
            {
                texts[j].text = "Cancel";
                break;
            }
            if (i != nowLevel)
                texts[j].text = "F" + i--;
            else
            {
                texts[j].text = "F" + --i;
                i--;
            }
        }
        i = 7;
        buttons = GetComponentsInChildren<Button>();
        for (int j = 0; j < buttons.Length; j++)
        {
            if (j == buttons.Length - 1)
            {
                buttons[j].onClick.AddListener(() =>
                {
                    ChangeSceneTip.Instance.hideChangeSceneTip();
                });
                break;
            }
            string floor;
            if (i != nowLevel)
            {
                floor = "F" + i--;
            }
            else
            {
                floor = "F" + --i;
                i--;
            }
            string currentFloor = floor;
            buttons[j].onClick.AddListener(() =>
            {
                SceneManager.LoadScene(currentFloor);
                ChangeSceneTip.Instance.hideChangeSceneTip();
                player.transform.position = GameObject.Find("stopPoint").transform.position;
            });
        }
    }
}
