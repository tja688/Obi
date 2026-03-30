using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractElevator : MonoBehaviour
{
    private bool isOnTrigger=false;
    public int nowLevel;
    public static InteractElevator Instance;
    private void Awake()
    {
        Instance = this;
    }
    private void Update()
    {
        if (isOnTrigger)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                ChangeSceneTip.Instance.showChangeSceneTip();
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            isOnTrigger=true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            isOnTrigger = false;
        }
    }
}
