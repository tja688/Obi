using System.Collections;
using TMPro;
using UnityEngine;

public class InteractDialog : MonoBehaviour
{
    public GameObject dialogPanel;
    public TextMeshProUGUI dialogNameTxt;
    public TextMeshProUGUI dialogContentTxt;
    public float typeSpeed=0.05f;
    public DialogData[] dialogDatas;
    public TextMeshProUGUI tip;
    private int currentDialogIndex=0;
    private bool isTypeComplete = false;
    private bool isOnTrigger = false;
    public static bool isOnDialog = false;
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
            tip.gameObject.SetActive(false);
            isOnTrigger = false;
            currentDialogIndex = 0;
            isTypeComplete=false;
        }
    }
    private void Update()
    {
        if (!isOnTrigger) return;
        if(isOnDialog) tip.gameObject.SetActive(false);
        else tip.gameObject.SetActive(true);
        if (Input.GetKeyDown(KeyCode.F))
        {
            isOnDialog = true;
            if (!dialogPanel.activeSelf)
            {
                dialogPanel.SetActive(true);
                ShowCurrentDialog();
            }
            else if (!isTypeComplete)
            {
                StopAllCoroutines();
                dialogContentTxt.text = dialogDatas[currentDialogIndex].content;
                isTypeComplete = true;
            }
            else
            {
                currentDialogIndex++;
                if (currentDialogIndex >= dialogDatas.Length)
                {
                    dialogPanel.SetActive(false);
                    isOnDialog = false;
                    currentDialogIndex = 0; // ÖØÖÃË÷Òý
                }
                else
                {
                    ShowCurrentDialog();
                }
            }
        }
    }
    private void ShowCurrentDialog()
    {
        if(currentDialogIndex>=dialogDatas.Length)
        {
            dialogPanel.SetActive(false);
        }
        dialogNameTxt.text= dialogDatas[currentDialogIndex].name;
        StartCoroutine(TypeEffect(dialogDatas[currentDialogIndex].content));
    }
    private IEnumerator TypeEffect(string text)
    {
        dialogContentTxt.text = "";
        isTypeComplete = false;

        for (int i = 0; i < text.Length; i++)
        {
            dialogContentTxt.text += text[i];
            yield return new WaitForSeconds(typeSpeed);
        }

        isTypeComplete = true;
    }
}
[System.Serializable]
public class DialogData
{
    public string name;
    public string content;
}
