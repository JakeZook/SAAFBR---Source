using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cabinet : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject ingredientToGive;
    [SerializeField] private PlayerPickup playerPickup;

    public void Interact()
    {
        if (ingredientToGive == null || playerPickup == null) return;

        GameObject spawnedIngredient = Instantiate(ingredientToGive, gameObject.transform.position, Quaternion.identity);
        playerPickup.PickupObject(spawnedIngredient);
    }
}
