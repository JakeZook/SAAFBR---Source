using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TV : MonoBehaviour
{
    [SerializeField] private GameObject[] orderCards;

    [Tooltip("The TV that overflow orders spill onto when this one is full. Leave empty on the last TV in the chain.")]
    [SerializeField] private TV nextTV;

    public int orderNum = 0;

    private float[] orderTimers;

    private void Awake()
    {
        orderTimers = new float[orderCards.Length];

        for (int i = 0; i < orderCards.Length; i++)
        {
            orderCards[i].SetActive(false);
        }
    }

    private void Update()
    {
        for (int i = 0; i < orderCards.Length; i++)
        {
            if (!orderCards[i].activeSelf)
                continue;

            orderTimers[i] += Time.deltaTime;

            TMP_Text timerText = orderCards[i]
                .transform.GetChild(3)
                .GetComponent<TMP_Text>();

            int minutes = Mathf.FloorToInt(orderTimers[i] / 60f);
            int seconds = Mathf.FloorToInt(orderTimers[i] % 60f);

            timerText.text = $"{minutes}:{seconds:00}";
        }
    }

    // Call this one on your FIRST TV only (the head of the chain).
    // It owns the master order-number counter; overflow TVs never
    // generate their own numbers, they just receive one.
    public (int, int) RenderOrderOnScreen(
        List<string> pattyTemps,
        List<IngredientSO> orderIngredients,
        float cost)
    {
        orderNum++;
        return PlaceOrder(orderNum, 0f, pattyTemps, orderIngredients, cost);
    }

    private (int, int) PlaceOrder(
        int number,
        float startTimer,
        List<string> pattyTemps,
        List<IngredientSO> orderIngredients,
        float cost)
    {
        int cardIndex = FindFreeCardIndex();

        if (cardIndex == -1)
        {
            if (nextTV != null)
                return nextTV.PlaceOrder(number, startTimer, pattyTemps, orderIngredients, cost);

            Debug.LogWarning("No free order cards on any TV!");
            return (-1, -1);
        }

        orderTimers[cardIndex] = startTimer;

        GameObject card = orderCards[cardIndex];
        card.SetActive(true);

        TMP_Text[] textBlocks = card.transform
            .GetChild(0)
            .GetComponentsInChildren<TMP_Text>(true);

        Transform orderNumParent = card.transform.GetChild(1);
        TMP_Text orderNumText = orderNumParent.GetComponent<TMP_Text>();

        Transform costParent = card.transform.GetChild(2);
        TMP_Text costText = costParent.GetComponent<TMP_Text>();

        foreach (TMP_Text text in textBlocks)
        {
            text.gameObject.SetActive(false);
            text.text = "";
        }

        int pattyIndex = 0;

        orderNumText.text = number.ToString();
        orderNumText.gameObject.SetActive(true);

        costText.text = "$" + cost.ToString("F2");
        costText.gameObject.SetActive(true);

        List<IngredientSO> ingredients = new List<IngredientSO>(orderIngredients);
        ingredients.Reverse();

        for (int i = 0; i < ingredients.Count && i < textBlocks.Length; i++)
        {
            IngredientSO ingredient = ingredients[i];

            string line = ingredient.ingredientName;

            if (ingredient.ingredientName == "Patty")
            {
                line += $" ({pattyTemps[pattyIndex]})";
                pattyIndex++;
            }

            textBlocks[i].text = line;
            textBlocks[i].gameObject.SetActive(true);
        }

        return (cardIndex, number);
    }

    // Call this one on whichever TV you like — it'll search the
    // whole chain for the order.
    public bool RemoveOrderOnScreen(int orderNumber)
    {
        int cardIndex = FindCardIndexForOrder(orderNumber);

        if (cardIndex == -1)
        {
            if (nextTV != null)
                return nextTV.RemoveOrderOnScreen(orderNumber);

            Debug.LogWarning($"Could not find Order {orderNumber} on any TV.");
            return false;
        }

        RemoveOrderAtIndex(cardIndex);
        return true;
    }

    // Shifts everything after `index` left within THIS TV, then
    // pulls the next TV's first order over to fill the gap we
    // just opened (which cascades down the chain).
    private void RemoveOrderAtIndex(int index)
    {
        orderCards[index].SetActive(false);
        orderTimers[index] = 0f;

        int lastShiftedIndex = index;

        for (int i = index; i < orderCards.Length - 1; i++)
        {
            if (!orderCards[i + 1].activeSelf)
                break;

            CopyCard(orderCards[i + 1], orderCards[i]);
            orderTimers[i] = orderTimers[i + 1];
            lastShiftedIndex = i + 1;
        }

        orderCards[lastShiftedIndex].SetActive(false);
        orderTimers[lastShiftedIndex] = 0f;

        PullOrderFromNextTV();
    }

    // If this TV now has a free slot and the next TV has an order
    // waiting, move that order's #1 spot over here.
    private void PullOrderFromNextTV()
    {
        if (nextTV == null)
            return;

        int freeIndex = FindFreeCardIndex();
        if (freeIndex == -1)
            return;

        if (!nextTV.orderCards[0].activeSelf)
            return;

        CopyCard(nextTV.orderCards[0], orderCards[freeIndex]);
        orderTimers[freeIndex] = nextTV.orderTimers[0];

        // This shifts nextTV's remaining orders left and recursively
        // pulls from ITS nextTV too, if one exists.
        nextTV.RemoveOrderAtIndex(0);
    }

    private int FindFreeCardIndex()
    {
        for (int i = 0; i < orderCards.Length; i++)
        {
            if (!orderCards[i].activeSelf)
                return i;
        }

        return -1;
    }

    private int FindCardIndexForOrder(int orderNumber)
    {
        for (int i = 0; i < orderCards.Length; i++)
        {
            if (!orderCards[i].activeSelf)
                continue;

            TMP_Text orderNumText = orderCards[i]
                .transform.GetChild(1)
                .GetComponent<TMP_Text>();

            if (int.TryParse(orderNumText.text, out int currentOrderNumber)
                && currentOrderNumber == orderNumber)
            {
                return i;
            }
        }

        return -1;
    }

    private void CopyCard(GameObject sourceCard, GameObject destinationCard)
    {
        TMP_Text[] sourceIngredientTexts = sourceCard
            .transform.GetChild(0)
            .GetComponentsInChildren<TMP_Text>(true);

        TMP_Text[] destinationIngredientTexts = destinationCard
            .transform.GetChild(0)
            .GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < destinationIngredientTexts.Length; i++)
        {
            if (i < sourceIngredientTexts.Length)
            {
                destinationIngredientTexts[i].text =
                    sourceIngredientTexts[i].text;

                destinationIngredientTexts[i].gameObject.SetActive(
                    sourceIngredientTexts[i].gameObject.activeSelf
                );
            }
            else
            {
                destinationIngredientTexts[i].text = "";
                destinationIngredientTexts[i].gameObject.SetActive(false);
            }
        }

        TMP_Text sourceOrderNumber = sourceCard
            .transform.GetChild(1)
            .GetComponent<TMP_Text>();

        TMP_Text destinationOrderNumber = destinationCard
            .transform.GetChild(1)
            .GetComponent<TMP_Text>();

        destinationOrderNumber.text = sourceOrderNumber.text;
        destinationOrderNumber.gameObject.SetActive(
            sourceOrderNumber.gameObject.activeSelf
        );

        TMP_Text sourceCost = sourceCard
            .transform.GetChild(2)
            .GetComponent<TMP_Text>();

        TMP_Text destinationCost = destinationCard
            .transform.GetChild(2)
            .GetComponent<TMP_Text>();

        destinationCost.text = sourceCost.text;
        destinationCost.gameObject.SetActive(
            sourceCost.gameObject.activeSelf
        );

        destinationCard.SetActive(true);
    }

    public float GetOrderTime(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= orderTimers.Length)
            return 0f;

        return orderTimers[cardIndex];
    }
}