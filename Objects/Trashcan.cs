using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trashcan : MonoBehaviour, IInteractable
{
    [SerializeField] GameObject[] dirtyVisuals;
    [SerializeField] private PlayerPickup playerPickup;
    [SerializeField] GameObject trashBagPrefab;
    private int dirtyIndex = 0;
    private int maxDirty = 3;

    private void OnCollisionEnter(Collision collision)
    {
        GameObject obj = collision.gameObject;
        StackableItem stackableItem = obj.GetComponent<StackableItem>();
        Choppable choppable = obj.GetComponent<Choppable>();
        BurgerStack burgerStack = obj.GetComponent<BurgerStack>();

        if (!hasRoom()) return;

        if (stackableItem != null || choppable != null) trashItem(obj);
        else if (burgerStack != null) trashStack(obj, burgerStack);
    }

    private void trashItem(GameObject obj)
    {
        Destroy(obj);
        addDirty();
    }

    private void trashStack(GameObject obj, BurgerStack stack)
    {
        stack.ClearStack();
        addDirty();
    }

    private bool hasRoom()
    {
        if (dirtyIndex >= maxDirty) return false;
        return true;
    }

    private void addDirty()
    {
        if (dirtyIndex >= dirtyVisuals.Length) return;

        dirtyVisuals[dirtyIndex].SetActive(true);
        dirtyIndex++;
    }

    public void Interact()
    {
        if (dirtyIndex < maxDirty) return;

        for (int i = 0; i < dirtyVisuals.Length; i++)
        {
            dirtyVisuals[i].SetActive(false);
        }

        dirtyIndex = 0;
        SpawnTrashBag();
    }

    private void SpawnTrashBag()
    {
        if (playerPickup == null) return;

        GameObject trashbag = Instantiate(trashBagPrefab);
        trashbag.transform.position = transform.position;

        playerPickup.PickupObject(trashbag);
    }

    public bool CanHighlight()
    {
        if (dirtyIndex >= maxDirty) return true;
        return false;
    }
}
