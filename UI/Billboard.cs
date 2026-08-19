using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private float yRotationOffset = 180f; // Flip the canvas

    private Transform mainCamera;

    void Start()
    {
        if (Camera.main != null)
            mainCamera = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        transform.LookAt(mainCamera.position);

        transform.Rotate(0f, yRotationOffset, 0f, Space.Self);
    }
}
