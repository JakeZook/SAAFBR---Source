using UnityEngine;

public class StackableItem : MonoBehaviour
{
    public IngredientSO ingredient;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if we hit a plate
        BurgerStack stack = collision.collider.GetComponentInParent<BurgerStack>();

        if (stack != null)
        {
            stack.StackIngredient(this, ingredient);
        }
    }
}
