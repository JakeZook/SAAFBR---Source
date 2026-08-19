using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCounter : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        BurgerStack stack = collision.collider.GetComponent<BurgerStack>();
        if (stack == null) return;

        stack.LogStackContents();
    }
}
