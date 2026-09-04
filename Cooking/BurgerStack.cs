using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BurgerStack : MonoBehaviour
{
    [Header("Stack Settings")]
    public Transform basePoint;
    public float ingredientHeight = 0.03f;
    private int stackSize = 0;
    public List<IngredientSO> ingredients = new List<IngredientSO>();
    public List<PattyData> pattyDataList = new List<PattyData>();
    public bool isComplete = false;
    public BoxCollider stackCollider;
    private IngredientSO lastAdded  = null;
    private Vector3 originalColliderSize;
    private Vector3 originalColliderCenter;

    public struct PattyData
    {
        public string topState;
        public string bottomState;

        public PattyData(string top, string bottom)
        {
            topState = top;
            bottomState = bottom;
        }

    }

    public bool isBaconCooked = true;

    private void Start()
    {
        stackCollider = GetComponent<BoxCollider>();
        if (stackCollider == null)
        {
            stackCollider = gameObject.AddComponent<BoxCollider>();
        }

        originalColliderSize = stackCollider.size;
        originalColliderCenter = stackCollider.center;
    }

    public void StackIngredient(StackableItem stackable, IngredientSO ingredient)
    {
        if (stackable == null || ingredient == null) return;

        // Check if can add
        if (!CheckIfCanAddIngredient(ingredient))
        {
            Debug.LogWarning($"Cannot stack ingredient: {ingredient.ingredientName}");
            return;
        }

        // Instantiate a copy of the ingredient for visuals
        GameObject visual = Instantiate(stackable.gameObject, transform);

        // Remove physics and StackableItem from visual
        Rigidbody rb = visual.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        StackableItem si = visual.GetComponent<StackableItem>();
        if (si != null) Destroy(si);

        BoxCollider col = visual.GetComponent<BoxCollider>();
        if (col != null) Destroy(col);

        Outline outline = visual.GetComponent<Outline>();
        if (outline != null) Destroy(outline);

        //Sets the height of the ingredient visual depending on given value for model - value within SO
        float yOffset = 0f;
        foreach (var ingr in ingredients)
        {
            yOffset += ingr.height;
        }

        //Sets position for models with strange pivot point - value given within SO
        visual.transform.position = basePoint.position + Vector3.up * yOffset;
        visual.transform.localPosition += ingredient.pivotOffset;

        //Makes sure ingredient is flat for models with rotated pivot
        if (stackSize == 0) visual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        else visual.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        Vector3 worldScale = stackable.transform.lossyScale;
        visual.transform.localScale = new Vector3(
            worldScale.x / transform.lossyScale.x,
            worldScale.y / transform.lossyScale.y,
            worldScale.z / transform.lossyScale.z
        );

        //Get temperature of the patty added and store it to the stack
        if (ingredient.ingredientName == "Patty") SavePattyData(stackable);

        if (ingredient.ingredientName == "Bacon") CheckIfBaconCooked(stackable);


        stackSize++;
        ingredients.Add(ingredient);
        //Reference to the last ingredient added - useful for burger stacking bug
        lastAdded = ingredient;

        //Update collision for burger stack now that it is taller
        UpdateStackCollider();

        // Destroy original ingredient GameObject
        Destroy(stackable.gameObject);
    }

    public void AddSauce(GameObject sauceVisual, IngredientSO ingredient)
    {
        if (ingredient == null) return;

        // Check if can add
        if (!CheckIfCanAddIngredient(ingredient))
        {
            Debug.LogWarning($"Cannot stack ingredient: {ingredient.ingredientName}");
            return;
        }

        if (DoesStackHaveSameSauce(ingredient)) return;

        GameObject visual = Instantiate(sauceVisual.gameObject, transform);

        //Sets the height of the ingredient visual depending on given value for model - value within SO
        float yOffset = 0f;
        foreach (var ingr in ingredients)
        {
            yOffset += ingr.height;
        }
        
        visual.transform.position = basePoint.position + Vector3.up * yOffset;
        visual.transform.localPosition += ingredient.pivotOffset;

        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Vector3 worldScale = sauceVisual.transform.lossyScale;

        visual.transform.localScale = new Vector3(
            worldScale.x / transform.lossyScale.x,
            worldScale.y / transform.lossyScale.y,
            worldScale.z / transform.lossyScale.z
        );

        stackSize++;
        ingredients.Add(ingredient);
        lastAdded = ingredient;

        UpdateStackCollider();
    }

    //Add temperature for both sides of patty added to stack and add it to stack data
    private void SavePattyData(StackableItem ingredient)
    {
        Patty patty = ingredient.GetComponent<Patty>();
        if (patty == null) return;

        PattyData data = new PattyData(
            patty.topTempState.ToString(),
            patty.bottomTempState.ToString()
        );

        pattyDataList.Add(data);
    }

    private void CheckIfBaconCooked(StackableItem ingredient)
    {
        Bacon bacon = ingredient.GetComponent<Bacon>();
        if (bacon == null) return;

        if (bacon.state == Bacon.BaconState.Cooked) isBaconCooked = true;
        else isBaconCooked = false;
    }

    private bool CheckIfCanAddIngredient(IngredientSO ingredient)
    {
        //Burger at max size
        if (stackSize >= 10)
        {
            return false;
        }

        //First has to be bottom bun
        if (stackSize == 0 && ingredient.ingredientName != "Bun")
        {
            return false;
        }

        //Prevent Putting top bun on without patty in stack
        if (ingredient.ingredientName == "Bun" && !DoesStackHavePatty() && DoesStackHaveBun())
        {
            return false;
        }

        //Prevents bun stacking
        if (lastAdded != null)
        {
            if (ingredient.ingredientName == "Bun" && lastAdded.ingredientName == "Bun")
            {
                return false;
            }
        }

        //If putting top bun, check for patty, then finish burger stack
        if (ingredient.ingredientName == "Bun" && DoesStackHavePatty())
        {
            isComplete = true;
            return true;
        }

        //Makes sure last possible item on stack is top bun
        if (stackSize == 9 && ingredient.ingredientName != "Bun")
        {
            return false;
        }

        //Prevents adding to complete burger
        if (isComplete)
        {
            return false;
        }

        return true;
    }

    //Check is burger stack already has a patty
    public bool DoesStackHavePatty()
    {
        foreach (var ingr in ingredients)
        {
            if (ingr.ingredientName == "Cooked Patty" || ingr.ingredientName == "Burned Patty" || ingr.ingredientName == "Raw Patty" || ingr.ingredientName == "Patty")
            {
                return true;
            }
        }
        return false;
    }

    //Check if burger stack already has a bun
    public bool DoesStackHaveBun()
    {
        foreach (var ingr in ingredients)
        {
            if (ingr.ingredientName == "Bun")
            {
                return true;
            }
        }
        return false;
    }

    public bool DoesStackHaveSameSauce(IngredientSO ingredient)
    {
        foreach (var ingr in ingredients)
        {
            if (ingr.ingredientName == ingredient.ingredientName)
            {
                return true;
            }
        }
        return false;
    }

    //Increases collider size when ingredients are added so more ingredients can go on
    private void UpdateStackCollider()
    {
        float totalHeight = 0f;
        foreach (var ingr in ingredients)
        {
            totalHeight += ingr.height;
        }

        Vector3 size = stackCollider.size;
        size.z = totalHeight / 2; 
        stackCollider.size = size;

        Vector3 center = stackCollider.center;
        center.z = size.z / 2f; 
        stackCollider.center = center;
    }

    //Logs everything on burger including patty temps
    public void LogStackContents()
    {
        Debug.Log("---- Burger Contents ----");

        for (int i = 0; i < ingredients.Count; i++)
        {
            Debug.Log($"Ingredient {i + 1}: {ingredients[i].name}");
        }

        for (int i = 0; i < pattyDataList.Count; i++)
        {
            PattyData patty = pattyDataList[i];
            Debug.Log($"Patty {i + 1} - Top: {patty.topState}, Bottom: {patty.bottomState}");
        }

        Debug.Log("-------------------------");
    }

    public List<IngredientSO> GetListOfIngredients()
    {
        return ingredients;
    }

    public List<PattyData> GetPattyData()
    {
        return pattyDataList;
    }

    public void ClearStack()
    {
        // Destroy all visual ingredient children under this stack
        for (int i = transform.childCount - 1; i >= 1; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        // Reset tracking data
        ingredients.Clear();
        pattyDataList.Clear();
        stackSize = 0;
        isComplete = false;
        isBaconCooked = true;
        lastAdded = null;

        // Reset collider back to empty stack size
        if (stackCollider != null)
        {
            stackCollider.size = originalColliderSize;
            stackCollider.center = originalColliderCenter;
        }
    }
}
