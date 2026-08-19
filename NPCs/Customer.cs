using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class Customer : MonoBehaviour, IInteractable
{
    private enum State
    {
        MovingToLine,   // walking to whatever spot the manager just assigned (queue spot or order point)
        WaitingInLine,  // idle, standing in the queue, waiting for the manager to advance us
        ReadyToOrder,   // at the order point, standing by for the player to click and take the order
        Ordering,       // at the order point, running the order flow
        MovingToSeat,   // walking to our table after ordering
        Sitting,        // seated, waiting for food
        Eating,         // food delivered, counting down before leaving
        Leaving         // walking to the despawn point
    }

    private State currentState;
    private Coroutine movementRoutine;

    [Header("Ordering")]
    [SerializeField] private RecipeSO[] recipes;
    private RecipeSO order;
    private List<IngredientSO> orderIngredients = new List<IngredientSO>();
    private List<string> pattyTemps = new List<string>();
    private float orderCost;
    private Outline outline;
    public static event Action dropOffOrder;
    private int orderNum;
    public GameObject orderTag;
    private float orderTime;
    private float speedyTime = 120f;
    private float slowTime = 360f;
    BurgerStack stack;

    [Header("AI")]
    public Transform sitPoint;
    public Transform despawnPoint;
    public Transform platePoint;
    public int tableIndex;
    public CustomerManager customerManager;
    private NavMeshAgent agent;
    private Animator animator;
    private CapsuleCollider hitbox;
    public CashManager cashManager;

    // Assigned by CustomerManager whenever the line shifts
    private Transform lineTarget;
    private bool isFrontOfLine;

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

        // Join the back of the queue - the manager will call SetLineTarget on us
        customerManager.RegisterCustomer(this);
    }

    private void Update()
    {
        bool isWalking = agent.velocity.magnitude > 0.1f;
        animator.SetBool("isWalking", isWalking);

        if (isWalking || currentState == State.Ordering) hitbox.enabled = false;
    }

    private void LateUpdate()
    {
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

    // Player clicks the customer to take their order. Only does anything once
    // they've actually reached the front of the line and are standing at the
    // order point - clicking them anywhere else in the queue does nothing.
    public void Interact()
    {
        if (currentState != State.ReadyToOrder) return;

        ChangeState(State.Ordering);
    }

    // ================= Queue / Line handling =================

    // Called by CustomerManager any time the line shifts: on spawn, and whenever
    // the front customer leaves and everyone behind steps up one spot.
    public void SetLineTarget(Transform target, bool isFront)
    {
        // Nothing actually changed for us (e.g. someone joined behind us) - don't
        // interrupt whatever we're doing.
        if (target == lineTarget) return;

        lineTarget = target;
        isFrontOfLine = isFront;
        ChangeState(State.MovingToLine);
    }

    private void OnArriveAtLineSpot()
    {
        hitbox.enabled = true;
        ChangeState(isFrontOfLine ? State.ReadyToOrder : State.WaitingInLine);
    }

    // ================= State machine core =================

    private void ChangeState(State newState)
    {
        ExitState(currentState);
        currentState = newState;
        EnterState(newState);
    }

    private void EnterState(State state)
    {
        switch (state)
        {
            case State.MovingToLine:
                StartMovement(lineTarget, OnArriveAtLineSpot);
                break;

            case State.WaitingInLine:
                // Just stands here until SetLineTarget is called again
                break;

            case State.ReadyToOrder:
                // Just stands here until the player clicks us - see Interact()
                break;

            case State.Ordering:
                StartCoroutine(HandleOrderFlow());
                break;

            case State.MovingToSeat:
                StartMovement(sitPoint, SitDown);
                break;

            case State.Sitting:
                // Waiting for a burger to be delivered - see OnCollisionEnter
                break;

            case State.Eating:
                StartCoroutine(EatFood());
                break;

            case State.Leaving:
                StartMovement(despawnPoint, () => Destroy(gameObject));
                break;
        }
    }

    private void ExitState(State state)
    {
        // Hook for per-state cleanup later if needed
    }

    private void StartMovement(Transform target, Action onArrive)
    {
        if (movementRoutine != null) StopCoroutine(movementRoutine);
        movementRoutine = StartCoroutine(MoveToPoint(target, onArrive));
    }

    // AI Path finding - goes to target given and invokes callback on arrival
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
        agent.ResetPath();

        onArrive?.Invoke();
    }

    // ================= Ordering =================

    private IEnumerator HandleOrderFlow()
    {
        orderIngredients.Clear();
        pattyTemps.Clear();

        animator.SetBool("isOrdering", true);

        int randomIndex = UnityEngine.Random.Range(0, recipes.Length);
        order = recipes[randomIndex];
        orderCost = order.cost;

        foreach (IngredientSO ingredient in order.recipe)
        {
            orderIngredients.Add(ingredient);
            if (ingredient.ingredientName == "Patty") SetPattyTemp();
        }

        (orderCardIndex, orderNum) = TV.RenderOrderOnScreen(pattyTemps, orderIngredients, orderCost);

        speechBubble.SetActive(true);
        SetSpeechBubbleUI();

        yield return StartCoroutine(WaitForAnimationFinish("Talking", "isOrdering", false));

        speechBubble.SetActive(false);

        // Free our spot in line - this triggers the manager to shift everyone behind us forward
        customerManager.AdvanceLine(this);

        ChangeState(State.MovingToSeat);
    }

    // Select at random temps for each ordered patty
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

    // At table and needs to sit down - ensures animation plays and facing correct orientation
    private void SitDown()
    {
        animator.SetBool("needsToSit", true);
        hitbox.enabled = true;

        StartCoroutine(WaitForAnimationFinish("Stand to Sit", "needsToSit", false));
        transform.rotation = sitPoint.rotation;

        TMP_Text orderTagNum = orderTag.GetComponentInChildren<TMP_Text>();
        orderTagNum.text = orderNum.ToString();

        ChangeState(State.Sitting);
    }

    // Helper to wait until animation is over before proceeding to next task
    private IEnumerator WaitForAnimationFinish(string animName, string parameter, bool value)
    {
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(animName)) yield return null;
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f) yield return null;

        animator.SetBool(parameter, value);
    }

    // Timer for how long customer stays at table after order delivery
    private IEnumerator EatFood()
    {
        yield return new WaitForSeconds(10f);

        animator.SetBool("needsToStand", true);

        yield return StartCoroutine(WaitForAnimationFinish("Sit to Stand", "needsToStand", false));

        ChangeState(State.Leaving);
    }

    // Collides with burger = order delivered
    private void OnCollisionEnter(Collision collision)
    {
        // Can only receive a burger while seated waiting for food
        if (currentState != State.Sitting) return;

        stack = collision.collider.GetComponent<BurgerStack>();
        if (stack == null) return;
        if (!stack.isComplete) return;

        GameObject burgerVisual = stack.gameObject;

        orderTime = TV.GetOrderTime(orderCardIndex);
        TV.RemoveOrderOnScreen(orderNum);

        dropOffOrder?.Invoke();
        PlaceBurgerOnTable(burgerVisual);

        CompareStack(stack.GetListOfIngredients(), stack.GetPattyData());
    }

    // Compare order given to order requested
    private void CompareStack(List<IngredientSO> ingredients, List<BurgerStack.PattyData> stackPattyTemps)
    {
        bool orderMatches = true;
        bool tempsMatch = true;

        if (orderIngredients.Count != ingredients.Count)
        {
            orderMatches = false;
        }

        int ingredientCount = Mathf.Min(orderIngredients.Count, ingredients.Count);

        for (int i = 0; i < ingredientCount; i++)
        {
            string needed = orderIngredients[i].ingredientName;
            string received = ingredients[i].ingredientName;

            if (needed != received) orderMatches = false;
        }

        int orderPattyIndex = 0;

        for (int i = 0; i < orderIngredients.Count; i++)
        {
            if (orderIngredients[i].ingredientName != "Patty")
                continue;

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

        if (stackPattyTemps.Count != orderPattyIndex) tempsMatch = false;

        hitbox.enabled = false;

        if (orderMatches && tempsMatch)
        {
            animator.SetBool("isThanking", true);
            StartCoroutine(WaitForAnimationFinish("Sitting Clap", "isThanking", false));
        }
        else
        {
            animator.SetBool("isYelling", true);
            StartCoroutine(WaitForAnimationFinish("Sitting Mad", "isYelling", false));
        }

        ChangeState(State.Eating);

        TMP_Text orderTagNum = orderTag.GetComponentInChildren<TMP_Text>();
        orderTagNum.text = "";

        float cashEarned = GetCashEarned(orderMatches, tempsMatch, orderPattyIndex);
        cashManager.UpdateCash(cashEarned, true);
    }

    // Checks if patty temps ordered are matching
    private bool CompareTemps(BurgerStack.PattyData tempData, string tempNeeded)
    {
        string top = tempData.topState.ToString();
        string bottom = tempData.bottomState.ToString();

        return top == tempNeeded && bottom == tempNeeded;
    }

    // Places burger on table
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

    // Show order on speech bubble
    void SetSpeechBubbleUI()
    {
        Transform container = speechBubble.transform.GetChild(0).GetChild(0);
        int pattyIndex = 0;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform item = container.GetChild(i);
            TMP_Text text = item.GetComponent<TMP_Text>();

            if (i < orderIngredients.Count && orderIngredients[i] != null)
            {
                item.gameObject.SetActive(true);
                text.text = orderIngredients[i].ingredientName;

                if (orderIngredients[i].ingredientName == "Patty")
                {
                    text.text += " - " + pattyTemps[pattyIndex];
                    pattyIndex++;
                }
            }
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

        if (!orderMatch) earnings -= orderCost * 0.40f;
        if (!tempsMatch) earnings -= 1.50f + (0.50f * numPattys);
        if (!stack.isBaconCooked) earnings -= .75f;

        float speedModifier = 0f;

        if (orderTime < speedyTime) speedModifier = Mathf.Clamp(0.75f + (numPattys * 0.25f), 1f, 1.5f);
        else if (orderTime > slowTime) speedModifier = -1f;

        earnings += speedModifier;
        earnings = Mathf.Max(0f, earnings);

        return earnings;
    }
}