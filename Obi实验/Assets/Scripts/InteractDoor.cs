using TMPro;
using UnityEngine;

public class InteractDoor : MonoBehaviour
{
    public Animator anim;
    public TextMeshProUGUI tip;
    private bool isOpen=false;
    private bool isOnTrigger = false;
    private void Update()
    {
        if (isOnTrigger)
        {
            if (Input.GetKeyDown(KeyCode.F) && isOpen == false)
            {
                anim.SetTrigger("open");
                isOpen = true;
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                anim.SetTrigger("close");
                isOpen = false;
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            isOnTrigger = true;
            tip.gameObject.SetActive(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            isOnTrigger = false;
            tip.gameObject.SetActive(false);
        }
    }
}
