using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sauce : MonoBehaviour
{
    public IngredientSO ingredient;
    [SerializeField] GameObject saucePrefab;
    
    private void OnTriggerEnter(Collider other) 
    {
        // Check if we hit a plate
        BurgerStack stack = other.GetComponentInParent<BurgerStack>();

        if (stack != null)
        {
            stack.AddSauce(saucePrefab, ingredient);
        }
    }
}
