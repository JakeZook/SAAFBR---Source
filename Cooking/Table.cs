using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Table : MonoBehaviour, IInteractable
{
    private Outline outline;

    [Header("Table Variable")]
    public bool needsClean = false;
    public int tableIndex;

    [Header("Refs")]
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private GameObject dirtyPlatePrefab;
    [SerializeField] private GameObject dirtSpecks;
    private GameObject platePoint;
    private GameObject dirtyPlate;
    [SerializeField] PlayerPickup playerPickup;

    private void Start()
    {
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        platePoint = transform.GetChild(0).gameObject;
    }

    public void Interact()
    {
        if (needsClean) CleanTable();
    }

    private void CleanTable()
    {
        needsClean = false;
        customerManager.EmptyTable(tableIndex);

        dirtyPlate.tag = "Pickup";
        playerPickup.PickupObject(dirtyPlate);

        Rigidbody rb = dirtyPlate.GetComponent<Rigidbody>();
        if (rb) rb.constraints = RigidbodyConstraints.FreezeRotation;

        dirtSpecks.SetActive(false);
    }

    //Puts used plate on table after ate
    public IEnumerator DirtyTable(GameObject burger)
    {
        yield return new WaitForSeconds(10f);
        Destroy(burger);

        dirtyPlate = Instantiate(dirtyPlatePrefab);
        dirtyPlate.transform.position = platePoint.transform.position;
        dirtyPlate.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);        
        dirtyPlate.tag = "Untagged";

        Rigidbody rb = dirtyPlate.GetComponent<Rigidbody>();
        if (rb) rb.constraints = RigidbodyConstraints.FreezeAll;

        dirtSpecks.SetActive(true);

        needsClean = true;
    }

    public bool CanHighlight()
    {
        if (needsClean) return true;
        return false;
    }
}
