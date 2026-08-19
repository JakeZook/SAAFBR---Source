using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sink : MonoBehaviour, IInteractable
{
    private int maxDirtyPlates = 4;
    private int maxCleanPlates = 4;
    private int dirtyPlateCount = 0;
    private int cleanPlateCount = 0;

    [SerializeField] GameObject platePrefab;
    [SerializeField] GameObject cleanPlatePrefab;
    [SerializeField] GameObject dirtyPlatePrefab;
    [SerializeField] GameObject dirtyPlacePoint;
    [SerializeField] GameObject cleanPlacePoint;
    [SerializeField] private float plateStackOffset = 0.005f;

    private List<GameObject> dirtyPlates = new List<GameObject>();
    private List<GameObject> cleanPlates = new List<GameObject>();

    private float cleanTime = 3f;
    private bool isCleaning = false;

    public void Interact()
    {
        if (isCleaning) return;
        WashDish();
    }

    private void WashDish()
    {
        if (cleanPlateCount >= maxCleanPlates || dirtyPlateCount == 0) return;
        GameObject topPlate = dirtyPlates[dirtyPlates.Count - 1];
        dirtyPlates.Remove(topPlate);
        dirtyPlateCount--;
        Destroy(topPlate);
        StartCoroutine(StartWashing());
    }

    private void OnCollisionEnter(Collision collision)
    {
        DirtyPlate dirtyPlate = collision.collider.GetComponent<DirtyPlate>();
        if (dirtyPlate == null) return;

        // Failsafe to prevent multiple triggers
        if (dirtyPlate.IsAddedToSink()) return;

        if (dirtyPlateCount >= maxDirtyPlates) return;

        dirtyPlate.addToSink();

        // Delete the dirty plate that was thrown/dropped into the sink.
        Destroy(dirtyPlate.gameObject);

        // Spawn the replacement dirty plate.
        GameObject newDirtyPlate = Instantiate(dirtyPlatePrefab, dirtyPlacePoint.transform.position, dirtyPlacePoint.transform.rotation);
        newDirtyPlate.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        newDirtyPlate.tag = "Untagged";
        Rigidbody rb = newDirtyPlate.GetComponent<Rigidbody>();
        if (rb) rb.constraints = RigidbodyConstraints.FreezeAll;
        BoxCollider bc = newDirtyPlate.GetComponent<BoxCollider>();
        bc.enabled = false;

        newDirtyPlate.transform.position += Vector3.up * (plateStackOffset * dirtyPlateCount);

        dirtyPlates.Add(newDirtyPlate);
        dirtyPlateCount++;
    }

    private void PlaceCleanDish()
    {
        //Spawn new plate
        GameObject newCleanPlate = Instantiate(cleanPlatePrefab, cleanPlacePoint.transform.position, cleanPlacePoint.transform.rotation);
        newCleanPlate.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        //Stack offset
        newCleanPlate.transform.position += Vector3.up * (plateStackOffset * cleanPlateCount);

        cleanPlates.Add(newCleanPlate);
        cleanPlateCount++;
    }

    private IEnumerator StartWashing()
    {
        isCleaning = true;
        yield return new WaitForSeconds(cleanTime);
        isCleaning = false;

        PlaceCleanDish();
    }

    public GameObject PickUpCLeanDish()
    {
        if (cleanPlateCount == 0) return null;
        GameObject topPlate = cleanPlates[cleanPlates.Count - 1];
        cleanPlates.Remove(topPlate);
        cleanPlateCount--;

        GameObject cleanPlateForPlayer = Instantiate(platePrefab, topPlate.transform.position, topPlate.transform.rotation);
        Destroy(topPlate);

        return cleanPlateForPlayer;
    }
}
