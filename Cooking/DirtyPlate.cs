using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DirtyPlate : MonoBehaviour
{
    private bool isAddedToSink = false;

    public void addToSink()
    {
        isAddedToSink = true;
    }

    public bool IsAddedToSink()
    {
        return isAddedToSink;
    }
}
