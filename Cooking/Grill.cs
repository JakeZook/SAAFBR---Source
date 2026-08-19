using UnityEngine;
using System.Collections.Generic;

public class Grill : MonoBehaviour
{
    [SerializeField] GameObject fire;
    
    public void StartFire()
    {
        fire.SetActive(true);
    }

    public void PutOutFire()
    {
        fire.SetActive(false);
    }
}
