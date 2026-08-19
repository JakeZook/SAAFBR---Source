using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class TestCustomer : MonoBehaviour, IInteractable
{
    [Header("Ordering")]
    [SerializeField] private RecipeSO[] recipes;
    private RecipeSO order;
    private List<IngredientSO> orderIngredients = new List<IngredientSO>();
    private List<string> pattyTemps = new List<string>();
    private float orderCost;
    private bool orderPlaced = false;
    private bool isOrdering = false;
    private Outline outline;
    public static event Action dropOffOrder;
    private int orderNum;
    public GameObject orderTag;
    private float orderTime;
    private float speedyTime = 120f;
    private float slowTime = 360f;
    BurgerStack stack;

    [Header("AI")]
    public Transform orderPoint;
    public Transform sitPoint;
    public Transform despawnPoint;
    public Transform platePoint;
    public int tableIndex;
    public CustomerManager customerManager;
    private NavMeshAgent agent;
    private Animator animator;
    private CapsuleCollider hitbox;
    public CashManager cashManager;

    [Header("UI")]
    [SerializeField] GameObject speechBubblePrefab;
    private GameObject speechBubble;
    public TV TV;
    int orderCardIndex;

    private void Start()
    {
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        hitbox = GetComponent<CapsuleCollider>();

        hitbox.enabled = false;

        speechBubble = Instantiate(speechBubblePrefab);
        speechBubble.SetActive(false);

        // Start first move immediately
        StartCoroutine(MoveToPoint(orderPoint, null));    
    }

    private void Update()
    {
        // Only animation logic stays in Update
        bool isWalking = agent.velocity.magnitude > 0.1f;
        animator.SetBool("isWalking", isWalking);
        if (isWalking || isOrdering) hitbox.enabled = false;
    }

    private void LateUpdate()
    {
        //If ordering, show speech bubble
        if (speechBubble != null && speechBubble.activeSelf)
        {
            Vector3 position = transform.position + Vector3.up * 1.5f; 
            speechBubble.transform.position = position;

            if (Camera.main != null)
            {
                Vector3 lookDir = Camera.main.transform.position - speechBubble.transform.position;
                lookDir.y = 0; 
                if (lookDir.sqrMagnitude > 0f) speechBubble.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }

    public void Interact()
    {
        PlaceOrder();
    }

    //When clicked on, decided order
    private void PlaceOrder()
    {
        //Prevents double ordering
        if (orderPlaced) return;

        //Failsafe - clears list before adding
        orderIngredients.Clear();
        pattyTemps.Clear();

        //Play ordering animation
        animator.SetBool("isOrdering", true);
        isOrdering = true;

        //Begin path finding to table and ordering procedure
        StartCoroutine(HandleOrderFlow());

        orderPlaced = true;
    }

    private IEnumerator HandleOrderFlow()
    {
        // Pick random recipe
        int randomIndex = UnityEngine.Random.Range(0, recipes.Length);
        order = recipes[randomIndex];
        orderCost = order.cost;

        foreach (IngredientSO ingredient in order.recipe)
        {
            orderIngredients.Add(ingredient);

            if (ingredient.ingredientName == "Patty") SetPattyTemp();
        }

        isOrdering = false;
        (orderCardIndex, orderNum) = TV.RenderOrderOnScreen(pattyTemps, orderIngredients, orderCost);

        speechBubble.SetActive(true);
        SetSpeechBubbleUI();

        // Wait for talking animation to finish
        yield return StartCoroutine(WaitForAnimationFinish("Talking", "isOrdering", false)
        );

        speechBubble.SetActive(false);

        // Walk to seat AFTER ordering
        StartCoroutine(MoveToPoint(sitPoint, SitDown));
    }

    //Select at random temps for each ordered patty
    private void SetPattyTemp()
    {
        int randomIndex = UnityEngine.Random.Range(0, 3);

        switch (randomIndex)
        {
            case 0: pattyTemps.Add("Rare"); break;
            case 1: pattyTemps.Add("Medium"); break;
            case 2: pattyTemps.Add("Well"); break;
        }
    }
    
    public void LogOrder()
    {
        int pattyIndex = 0;

        Debug.Log("---- Customer Order ----");

        for (int i = 0; i < orderIngredients.Count; i++)
        {
            IngredientSO ingredient = orderIngredients[i];
            string logText = $"#{i + 1} - {ingredient.ingredientName}";

            if (ingredient.ingredientName == "Patty")
            {
                logText += $" - Temp: {pattyTemps[pattyIndex]}";
                pattyIndex++;
            }

            Debug.Log(logText);
        }

        Debug.Log("------------------------");
    }

    //AI Path finding - goes to target given and plays given animation when arrives
    private IEnumerator MoveToPoint(Transform target, Action onArrive)
    {
        agent.isStopped = false;
        agent.SetDestination(target.position);

        while (agent.pathPending) yield return null;

        while (agent.remainingDistance > agent.stoppingDistance || agent.velocity.sqrMagnitude > 0.01f)
        {
            yield return null;
        }

        agent.isStopped = true;
        hitbox.enabled = true;
        agent.ResetPath();

        onArrive?.Invoke();
    }

    //At table and needs to sit down - ensures animation plays and facing correct orientation
    private void SitDown()
    {
        animator.SetBool("needsToSit", true);

        StartCoroutine(WaitForAnimationFinish("Stand to Sit", "needsToSit", false));
        transform.rotation = sitPoint.rotation;

        //Add order num from tag
        TMP_Text orderTagNum = orderTag.GetComponentInChildren<TMP_Text>();
        orderTagNum.text = orderNum.ToString();
    }

    //Helper to wait until animation is over before proceeding to next task
    private IEnumerator WaitForAnimationFinish(string animName, string parameter, bool value)
    {
        // Wait until animation starts
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(animName)) yield return null;

        // Wait until it finishes
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f) yield return null;

        animator.SetBool(parameter, value);
    }

    //Timer for how long customer stays at table after order delivery
    private IEnumerator EatFood()
    {
        yield return new WaitForSeconds(10f);

        animator.SetBool("needsToStand", true);

        yield return StartCoroutine(WaitForAnimationFinish("Sit to Stand", "needsToStand", false));

        // Customer has now stood up and is leaving
        yield return StartCoroutine(MoveToPoint(despawnPoint, null));

        Destroy(gameObject);
    }

    //Collides with burger = order delivered
    private void OnCollisionEnter(Collision collision)
    {
        //Can't deliver to customers who have not ordered
        if (!orderPlaced) return;

        //Makes sure it is a burger
        stack = collision.collider.GetComponent<BurgerStack>();
        if (stack == null) return;

        //Makes sure the burger is complete
        if (!stack.isComplete) return;

        //Get the visual of the burger game object
        GameObject burgerVisual = stack.gameObject;

        //Remove order from TV
        orderTime = TV.GetOrderTime(orderCardIndex);
        TV.RemoveOrderOnScreen(orderNum);

        //Run script for eating and place visual of order on table
        dropOffOrder?.Invoke();
        PlaceBurgerOnTable(burgerVisual);

        //Compare order given to order requested
        CompareStack(stack.GetListOfIngredients(), stack.GetPattyData());
    }

    //Compare order given to order requested
    private void CompareStack(List<IngredientSO> ingredients, List<BurgerStack.PattyData> stackPattyTemps)
    {
        bool orderMatches = true;
        bool tempsMatch = true;

        if (orderIngredients.Count != ingredients.Count)
        {
            orderMatches = false;
        }

        int ingredientCount = Mathf.Min(
            orderIngredients.Count,
            ingredients.Count
        );

        for (int i = 0; i < ingredientCount; i++)
        {
            string needed = orderIngredients[i].ingredientName;
            string received = ingredients[i].ingredientName;

            if (needed != received) orderMatches = false;
        }

        int orderPattyIndex = 0;

        for (int i = 0; i < orderIngredients.Count; i++)
        {
            // Not a patty
            if (orderIngredients[i].ingredientName != "Patty")
                continue;

            // Burger doesn't have this patty
            if (orderPattyIndex >= stackPattyTemps.Count)
            {
                tempsMatch = false;
                break;
            }

            string neededTemp = pattyTemps[orderPattyIndex];

            int stackPattyIndex = stackPattyTemps.Count - 1 - orderPattyIndex;

            bool thisPattyMatches = CompareTemps(stackPattyTemps[stackPattyIndex], neededTemp);

            if (!thisPattyMatches) tempsMatch = false;

            orderPattyIndex++;
        }

        // Burger has a different number of patties
        if (stackPattyTemps.Count != orderPattyIndex) tempsMatch = false;

        if (orderMatches && tempsMatch)
        {
            animator.SetBool("isThanking", true);
            hitbox.enabled = false;

            StartCoroutine(WaitForAnimationFinish("Sitting Clap", "isThanking", false));

            StartCoroutine(EatFood());
        }
        else
        {
            animator.SetBool("isYelling", true);
            hitbox.enabled = false;

            StartCoroutine(WaitForAnimationFinish("Sitting Mad", "isYelling", false));

            StartCoroutine(EatFood());
        }

        // Remove order number from tag
        TMP_Text orderTagNum = orderTag.GetComponentInChildren<TMP_Text>();
        orderTagNum.text = "";

        // Get cash
        float cashEarned = GetCashEarned(orderMatches, tempsMatch, orderPattyIndex);

        cashManager.UpdateCash(cashEarned, true);
    }

    //Checks is patty temps ordered are matching
    private bool CompareTemps(BurgerStack.PattyData tempData, string tempNeeded)
    {
        string top = tempData.topState.ToString();
        string bottom = tempData.bottomState.ToString();

        return top == tempNeeded && bottom == tempNeeded;
    }

    //Places burger on table
    void PlaceBurgerOnTable(GameObject burger)
    {
        burger.transform.position = platePoint.transform.position;
        burger.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);        
        burger.tag = "Untagged";
        
        Rigidbody rb = burger.GetComponent<Rigidbody>();
        if (rb) rb.constraints = RigidbodyConstraints.FreezeAll;
        
        Table table = platePoint.GetComponentInParent<Table>();
        table.tableIndex = tableIndex;
        if (table != null) StartCoroutine(table.DirtyTable(burger));
    }

    //Show order on speech bubble
    void SetSpeechBubbleUI()
    {
        //Get all possible order texts on UI
        Transform container = speechBubble.transform.GetChild(0).GetChild(0);
        //Keep track of what patty order is on
        int pattyIndex = 0;

        for (int i = 0; i < container.childCount; i++)
        {
            //Gets the text from each child
            Transform item = container.GetChild(i);
            TMP_Text text = item.GetComponent<TMP_Text>();

            //If still ingredients
            if (i < orderIngredients.Count && orderIngredients[i] != null)
            {
                item.gameObject.SetActive(true);
                text.text = orderIngredients[i].ingredientName;

                //If patty, show temps also
                if (orderIngredients[i].ingredientName == "Patty")
                {
                    text.text += " - " + pattyTemps[pattyIndex];
                    pattyIndex++;
                }
            }
            //If no more ingredients
            else
            {
                item.gameObject.SetActive(false);
            }
        }
    }

    float GetCashEarned(bool orderMatch, bool tempsMatch, int numPattys)
    {
        // - Pricing Matrix
        //🟢 Correct + correct temp + speedy = $6.00
        //🟢 Correct + correct temp + normal = $5.00
        //🟡 Correct + correct temp + slow = $4.00
        //🟡 Correct + wrong temp + speedy = $4.50
        //🟠 Correct + wrong temp + normal = $3.50
        //🟠 Correct + wrong temp + slow = $2.50
        //🟠 Wrong order + correct temp + speedy = $4.00
        //🔴 Wrong order + correct temp + normal = $3.00
        //🔴 Wrong order + correct temp + slow = $2.00
        //🔴 Wrong order + wrong temp + speedy = $2.50
        //🔴 Wrong order + wrong temp + normal = $1.50
        //🔴 Wrong order + wrong temp + slow = $0.50

        float earnings = orderCost;

        if (!orderMatch) earnings -= orderCost * 0.40f; // 40% penalty

        if (!tempsMatch) earnings -= 1.50f + (0.50f * numPattys); // -$1.50 for wrong temp plus .50 for each additional patty

        if (!stack.isBaconCooked) earnings -= .75f; //Free bacon if not cooked or burnt
        
        float speedModifier = 0f;

        if (orderTime < speedyTime) speedModifier = Mathf.Clamp(0.75f + (numPattys * 0.25f), 1f, 1.5f);

        else if (orderTime > slowTime) speedModifier = -1f;

        earnings += speedModifier;

        earnings = Mathf.Max(0f, earnings); //Prevents negatives

        return earnings;
    }
}