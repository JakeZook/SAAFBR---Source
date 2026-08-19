using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CashManager : MonoBehaviour
{
    TMP_Text cashText;
    private float cashHeld;

    private void Start()
    {
        cashText = GetComponentInChildren<TMP_Text>();
        cashText.text = "$0";
    }

    public void UpdateCash(float amount, bool isAdding)
    {
        cashHeld = isAdding ? cashHeld + amount : cashHeld - amount;

        cashText.text = "$" + cashHeld.ToString("F2");
        
        Debug.Log((isAdding ? "Adding ": "Removing ") + amount + " to bank");
    }
}
