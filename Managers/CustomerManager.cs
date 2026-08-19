using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    [Header("Placements")]
    [SerializeField] Transform spawnPoint;
    [SerializeField] Transform orderPoint;
    [SerializeField] GameObject[] seats;
    public bool[] openTables;
    [SerializeField] TV TV;
    [SerializeField] CashManager cashManager;

    [Header("NPC Model Customization")]
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private Material[] eyeMats;
    [SerializeField] private Material[] hairMats;
    [SerializeField] private Material[] pantsMats;
    [SerializeField] private Material[] shirtMats;
    [SerializeField] private Material[] shoeMats;
    [SerializeField] private Material[] skinMats;

    //Actually randomize seed on play
    private void Awake()
    {
        Random.InitState(System.Environment.TickCount);
    }

    private void Start()
    {
        // Set all tables to open
        openTables = new bool[seats.Length];

        for (int i = 0; i < seats.Length; i++)
        {
            openTables[i] = true;

            // Automatically give each Table its correct index
            Table table = seats[i].GetComponent<Table>();

            if (table != null)
            {
                table.tableIndex = i;
            }
        }
    }

    //Test block - spawn customer if space
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {

            if (IsTableOpen()) SpawnCustomer();
            else Debug.Log("No tables!");
        }
    }

    //Check if there are any open tables
    bool IsTableOpen()
    {
        for (int i = 0; i < openTables.Length; i++)
        {
            if (openTables[i] == true) return true;
        }
        return false;
    }

    void SpawnCustomer()
    {
        //Get npc prefab and all provided mats
        GameObject npc = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
        SkinnedMeshRenderer rend = npc.GetComponentInChildren<SkinnedMeshRenderer>();
        Material[] mats = rend.materials;

        //Pick at random mats for chosen character model
        for (int i = 0; i < mats.Length; i++)
        {
            string matName = mats[i].name;

            if (matName.Contains("Eyes"))
                {
                    mats[i] = eyeMats[Random.Range(0, eyeMats.Length)];
                }
                else if (matName.Contains("Hair"))
                {
                    mats[i] = hairMats[Random.Range(0, hairMats.Length)];
                }
                else if (matName.Contains("Pants"))
                {
                    mats[i] = pantsMats[Random.Range(0, pantsMats.Length)];
                }
                else if (matName.Contains("Shirt"))
                {
                    mats[i] = shirtMats[Random.Range(0, shirtMats.Length)];
                }
                else if (matName.Contains("Shoes"))
                {
                    mats[i] = shoeMats[Random.Range(0, shoeMats.Length)];
                }
                else if (matName.Contains("Skin"))
                {
                    mats[i] = skinMats[Random.Range(0, skinMats.Length)];
                }
        }

        rend.materials = mats;

        //Select an open table to sit at and change array index to false
        var table = PickTable();
        openTables[table.tableIndex] = false;

        //Set variables on specific customer for AI path finding
        TestCustomer script = npc.GetComponent<TestCustomer>();
        if (script) 
        {
            script.orderPoint = orderPoint;
            script.sitPoint = table.sitPoint;
            script.despawnPoint = spawnPoint;
            script.platePoint = table.platePoint;
            script.tableIndex = table.tableIndex;
            script.customerManager = this;
            script.orderTag = table.orderTag.gameObject;

            //Passes all tvs for order render
            script.TV = TV;
        }

        if (cashManager) script.cashManager = cashManager;
    }

    //Pick table out of list of available tables
    (Transform sitPoint,Transform platePoint, int tableIndex, Transform orderTag) PickTable()
    {
        List<int> possibleTables = new List<int>();

        //If table is open, add to possible list
        for (int i = 0; i < openTables.Length; i++)
        {
            if (openTables[i])
            {
                possibleTables.Add(i);
            }
        }

        //Failsafe if no tables are open - shouldn't run since check was earlier
        if (possibleTables.Count == 0) return (null, null, 0, null);

        //Select random out of list
        int randomListIndex = Random.Range(0, possibleTables.Count);
        int chosenIndex = possibleTables[randomListIndex];

        //Get the sitting point and plate point of the seat chosen
        GameObject tableChosen = seats[chosenIndex].gameObject;
        Transform sitPoint = tableChosen.transform.GetChild(0).GetChild(0).transform;
        Transform platePoint = tableChosen.transform.GetChild(1).GetChild(0).transform;
        Transform orderTag = tableChosen.transform.GetChild(1).GetChild(2).transform;
        int tableIndex = chosenIndex;
        return (sitPoint, platePoint, tableIndex, orderTag);
    }

    public void EmptyTable(int index)
    {
        openTables[index] = true;
    }
}
