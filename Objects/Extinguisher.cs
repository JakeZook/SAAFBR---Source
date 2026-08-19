using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Extinguisher : MonoBehaviour, IUseable
{
    private ExtinguisherSteam steam;
    private bool isBeingUsed;
    [SerializeField] Quaternion rotationOffset = Quaternion.Euler(90f, 0f, 0f);
    [SerializeField] Vector3 transformOffset = new Vector3(0f, 0f, 0f);

    private void Start()
    {
        steam = GetComponentInChildren<ExtinguisherSteam>();
    }

    private void Update()
    {
        if (steam == null) return;

        if (!isBeingUsed)
        {
            steam.StopSteam();
        }

        isBeingUsed = false;
    }

    public void Use()
    {
        if (steam == null) return;

        isBeingUsed = true;
        steam.StartSteam();
    }

    public Quaternion GetRotationOffset()
    {
        return rotationOffset;
    }

    public Vector3 GetTransformOffset()
    {
        return transformOffset;
    }
}
