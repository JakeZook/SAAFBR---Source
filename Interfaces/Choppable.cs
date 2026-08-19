using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Choppable : MonoBehaviour
{
    [SerializeField] private GameObject ingredientToSpawn;

    public GameObject GetIngredientToSpawn()
    {
        return ingredientToSpawn;
    }
}
