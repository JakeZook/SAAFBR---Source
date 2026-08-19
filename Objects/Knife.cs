using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Knife : MonoBehaviour, IUseable
{
    [SerializeField] Quaternion rotationOffset = Quaternion.Euler(90f, 0f, 0f);
    [SerializeField] Vector3 transformOffset = new Vector3(0f, 0f, 0f);
    private Choppable choppable;

    public void Use()
    {
        if (choppable == null) return;

        ChopObject();
    }

    public Quaternion GetRotationOffset()
    {
        return rotationOffset;
    }

    public Vector3 GetTransformOffset()
    {
        return transformOffset;
    }

    private void OnTriggerEnter(Collider other)
    {
        choppable = other.GetComponent<Choppable>();
    }

    private void ChopObject()
    {
        GameObject ingredient = Instantiate(choppable.GetIngredientToSpawn());
        ingredient.transform.position = choppable.gameObject.transform.position;
        ingredient.transform.rotation = choppable.gameObject.transform.rotation;
        Destroy(choppable.gameObject);
        choppable = null;
    }
}
