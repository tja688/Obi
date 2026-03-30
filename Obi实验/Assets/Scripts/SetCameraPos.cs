using UnityEngine;

public class SetCameraPos : MonoBehaviour
{
    public Transform cameraTransform;
    public Vector3 setCameraPosition;
    public Quaternion setCameraRotation;
    private void Awake()
    {
        cameraTransform = GameObject.Find("Camera").transform;
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.tag == "Player")
        {
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, setCameraPosition, 0.125f);
            cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation,setCameraRotation,0.125f);
        }
    }
}
