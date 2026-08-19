using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TV : MonoBehaviour
{
    [SerializeField] private GameObject[] orderCards;

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

    public (int, int) RenderOrderOnScreen(
        List<string> pattyTemps,
        List<IngredientSO> orderIngredients,
        float cost)
    {
        // Find the first empty card.
        // Orders always fill from left to right.
        int cardIndex = -1;

        for (int i = 0; i < orderCards.Length; i++)
        {
            if (!orderCards[i].activeSelf)
            {
                cardIndex = i;
                break;
            }
        }

        if (cardIndex == -1)
        {
            Debug.LogWarning("No free order cards!");
            return (-1, -1);
        }

        // Reset the timer for this order.
        orderTimers[cardIndex] = 0f;

        GameObject card = orderCards[cardIndex];

        card.SetActive(true);

        TMP_Text[] textBlocks = card.transform
            .GetChild(0)
            .GetComponentsInChildren<TMP_Text>(true);

        Transform orderNumParent = card.transform.GetChild(1);
        TMP_Text orderNumText = orderNumParent.GetComponent<TMP_Text>();

        Transform costParent = card.transform.GetChild(2);
        TMP_Text costText = costParent.GetComponent<TMP_Text>();

        // Hide and clear all ingredient text.
        foreach (TMP_Text text in textBlocks)
        {
            text.gameObject.SetActive(false);
            text.text = "";
        }

        int pattyIndex = 0;

        orderNum++;

        orderNumText.text = orderNum.ToString();
        orderNumText.gameObject.SetActive(true);

        costText.text = "$" + cost.ToString("F2");
        costText.gameObject.SetActive(true);

        // Reverse ingredients for the UI.
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

        return (cardIndex, orderNum);
    }

    public void RemoveOrderOnScreen(int orderNumber)
    {
        int cardIndex = -1;

        for (int i = 0; i < orderCards.Length; i++)
        {
            if (!orderCards[i].activeSelf)
                continue;

            TMP_Text orderNumText = orderCards[i]
                .transform.GetChild(1)
                .GetComponent<TMP_Text>();

            if (int.TryParse(orderNumText.text, out int currentOrderNumber))
            {
                if (currentOrderNumber == orderNumber)
                {
                    cardIndex = i;
                    break;
                }
            }
        }

        if (cardIndex == -1)
        {
            Debug.LogWarning($"Could not find Order {orderNumber} on the TV.");
            return;
        }

        orderCards[cardIndex].SetActive(false);
        orderTimers[cardIndex] = 0f;

        // Shift everything after cardIndex one slot left,
        // tracking the last slot that actually got copied into.
        int lastShiftedIndex = cardIndex;

        for (int i = cardIndex; i < orderCards.Length - 1; i++)
        {
            if (!orderCards[i + 1].activeSelf)
                break;

            CopyCard(orderCards[i + 1], orderCards[i]);
            orderTimers[i] = orderTimers[i + 1];
            lastShiftedIndex = i + 1;
        }

        // Clear the card whose data just got duplicated one slot left,
        // NOT always the last slot in the array.
        orderCards[lastShiftedIndex].SetActive(false);
        orderTimers[lastShiftedIndex] = 0f;
    }

    private void CopyCard(GameObject sourceCard, GameObject destinationCard)
    {
        TMP_Text[] sourceIngredientTexts = sourceCard
            .transform.GetChild(0)
            .GetComponentsInChildren<TMP_Text>(true);

        TMP_Text[] destinationIngredientTexts = destinationCard
            .transform.GetChild(0)
            .GetComponentsInChildren<TMP_Text>(true);

        // Copy ingredient text and active state.
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

        // Copy order number.
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

        // Copy cost.
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