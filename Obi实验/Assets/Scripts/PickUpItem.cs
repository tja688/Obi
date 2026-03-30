using System.Collections;
using TMPro;
using UnityEngine;

public class PickUpItem : MonoBehaviour
{
    public ItemData item;
    public TextMeshProUGUI tip;
    bool isOnTrigger=false;
    public TextMeshProUGUI pickUpTip;
    private Coroutine fadeCoroutine;
    private void OnTriggerEnter(Collider other)
    {
        if(other.tag=="Player")
        {
            isOnTrigger = true;
            tip.gameObject.SetActive(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if(other.tag=="Player")
        {
            isOnTrigger = false;
            tip.gameObject.SetActive(false);
        }
    }
    private void Update()
    {
        if (isOnTrigger)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                PlayerController.items.Add(item);
                PlayerController.Instance.UpdateShowItem();
                tip.gameObject.SetActive(false);
                PickUpEffect();
                this.gameObject.transform.position = new Vector3(999, 999, 999);
                Destroy(gameObject,4f);
            }
        }
    }
    private void PickUpEffect()
    {
        pickUpTip.gameObject.SetActive(true);
        pickUpTip.text = "Pick up the " + item.name;
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeRoutine());

    }
    IEnumerator FadeRoutine()
    {
        pickUpTip.alpha = 0;
        yield return StartCoroutine(Fade(0, 1));
        yield return new WaitForSeconds(1);
        yield return StartCoroutine(Fade(1, 0));
        pickUpTip.gameObject.SetActive(false);
    }
    IEnumerator Fade(float start,float end)
    {
        float time = 0;
        pickUpTip.alpha = start;
        while (time < 1)
        {
            time += Time.deltaTime;
            pickUpTip.alpha = Mathf.Lerp(start, end, time);
            yield return null;
        }
        pickUpTip.alpha = end;
    }
}
