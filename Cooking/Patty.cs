using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Patty : MonoBehaviour, IInteractable
{
    public enum PattyState { Raw, Cooking, Cooked, Burnt }
    public PattyState state = PattyState.Raw;
    public enum PattyTempState { Raw, Rare, Medium, Well, Burnt }
    public PattyTempState topTempState, bottomTempState = PattyTempState.Raw;

    [Header("References")]
    public Rigidbody rb;
    private Renderer rend;
    private PlayerPickup playerPickup;
    private Grill grill;

    [Header("Flip Settings")]
    private bool flipping = false;
    public float flipSpeed = 5f;
    private bool flipped = false;

    [Header("Cooking Settings")]
    private float cookProgressTop = 0f;
    private float cookProgressBottom = 0f;
    private float minCookTime = 20f;
    private float maxRareTime = 30f;
    private float maxMediumTime = 40f;
    private float maxWellTime = 50f;

    public bool isOnGrill = false;
    private bool hasStartedFire = false;

    [Header("Progress Bar")]
    [SerializeField] private GameObject progressBarPrefab; 
    private GameObject progressBarHolder;
    [SerializeField] private Image topBar;
    [SerializeField] private Image bottomBar;
    private Color32 rawColor = new Color32(255, 0, 215, 255);
    private Color32 rareColor = new Color32(0, 255, 0, 255);
    private Color32 mediumColor = new Color32(255, 239, 0, 255);
    private Color32 wellColor = new Color32(255, 94, 0, 255);
    private Color32 burntColor = new Color32(255, 2, 0, 255);

    [Header("Materials")]
    [SerializeField] private Material cookedMat;
    [SerializeField] private Material burntMat;

    [Header("Steam Particle")]
    [SerializeField] private GameObject steamPrefab; 
    private GameObject steamInstance;
    [SerializeField] private Color burntSteamColor = new Color(0f, 0f, 0f, 0.8f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();

        if (progressBarPrefab != null)
        {
            //Also spawn cooking progress bar, hide until on grill
            progressBarHolder = Instantiate(progressBarPrefab);
            progressBarHolder.SetActive(false);

            Transform holder = progressBarHolder.transform.Find("Holder");
            if (holder != null)
            {
                topBar = holder.Find("TopBar")?.GetComponent<Image>();
                bottomBar = holder.Find("BottomBar")?.GetComponent<Image>();
            }
        }
    }

    private void Update()
    {
        //While on grill, cook!
        if (isOnGrill)
        {
            if (flipped) cookProgressTop += Time.deltaTime;
            else cookProgressBottom += Time.deltaTime;

            //Update visuals and data each frame
            UpdateState();
            UpdateProgressBars();
        }
    }

    private void LateUpdate()
    {
        //If cooking, increase progress bar visual and face to player
        if (progressBarHolder != null && progressBarHolder.activeSelf)
        {
            Vector3 barPosition = transform.position + Vector3.up * 0.3f; 
            progressBarHolder.transform.position = barPosition;

            if (Camera.main != null)
            {
                Vector3 lookDir = Camera.main.transform.position - progressBarHolder.transform.position;
                lookDir.y = 0; 
                if (lookDir.sqrMagnitude > 0f) progressBarHolder.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Grill"))
        {
            grill = collision.collider.GetComponentInParent<Grill>();
            //Lock position on grill so no patties spin it
            isOnGrill = true;
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            //Drop from players hands when make contact
            playerPickup?.DropObject();

            //Turn on progress bar
            if (progressBarHolder != null) progressBarHolder.SetActive(true);

            //Show steam effect
            if (steamPrefab != null && steamInstance == null)
            {
                steamInstance = Instantiate(steamPrefab);
                steamInstance.transform.position = transform.position - transform.forward * 0.1f - transform.right * 0.05f - transform.up * 0.06f;
                steamInstance.transform.parent = transform;
                if (state == PattyState.Burnt)
                {
                    ParticleSystem steamParticles = steamInstance.GetComponent<ParticleSystem>();
                    if (steamParticles != null)
                    {
                        var main = steamParticles.main;
                        main.startColor = burntSteamColor;
                    }
                }
            }
        }
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag("Fire"))
        {
            bottomTempState = PattyTempState.Burnt;
            topTempState = PattyTempState.Burnt;
            cookProgressTop = 60f;
            cookProgressBottom = 60f;
        }
    }

    //Reverse everything when taken off grill
    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Grill"))
        {
            grill = null;
            isOnGrill = false;
            rb.constraints = RigidbodyConstraints.None;

            if (progressBarHolder != null) progressBarHolder.SetActive(false);

            if (steamInstance != null)
            {
                Destroy(steamInstance);
                steamInstance = null;
            }
        }
    }

    public void Interact()
    {
        Flip();
    }

    private void UpdateState()
    {
        //Decide when patty is cooked or burnt
        if (cookProgressTop >= minCookTime && cookProgressBottom >= minCookTime) state = PattyState.Cooked;
        if (cookProgressTop > maxWellTime || cookProgressBottom > maxWellTime) state = PattyState.Burnt;

        //Change texture on model to reflect state
        if (rend != null)
        {
            if (state == PattyState.Cooked) rend.material = cookedMat;
            if (state == PattyState.Burnt) rend.material = burntMat;
        }

        //Turn off steam when burnt
        if (state == PattyState.Burnt)
        {
            ParticleSystem steamParticles = steamInstance.GetComponent<ParticleSystem>();
            if (steamParticles != null)
            {
                var main = steamParticles.main;
                main.startColor = burntSteamColor;
            }
        }

        //Get current time cooked
        bottomTempState = GetTemperature(cookProgressBottom);
        topTempState = GetTemperature(cookProgressTop);

        //Change color of progress bar to show state
        UpdateProgressBarColor(bottomBar, bottomTempState);
        UpdateProgressBarColor(topBar, topTempState);
        
        if (bottomTempState == PattyTempState.Burnt || topTempState == PattyTempState.Burnt)
        {
            if (!grill) return;
            if (hasStartedFire) return;

            grill.StartFire();
            hasStartedFire = true;
        }
    }

    private void UpdateProgressBars()
    {
        //Increase bar while on grill
        if (topBar != null) topBar.fillAmount = Mathf.Clamp01(cookProgressTop / maxWellTime);
        if (bottomBar != null) bottomBar.fillAmount = Mathf.Clamp01(cookProgressBottom / maxWellTime);
    }

    private void UpdateProgressBarColor(Image bar, PattyTempState state)
    {
        //Purple = rare, Green = raw, Yellow = medium, Red = burnt
        if (state == PattyTempState.Raw) bar.color = rawColor;
        else if (state == PattyTempState.Rare) bar.color = rareColor;
        else if (state == PattyTempState.Medium) bar.color = mediumColor;
        else if (state == PattyTempState.Well) bar.color = wellColor;
        else if (state == PattyTempState.Burnt) bar.color = burntColor;
    }

    //Send time on grill for both sides of patty
    public PattyTempState GetTemperature(float sideProgress)
    {
        if (sideProgress < minCookTime) return PattyTempState.Raw;
        if (sideProgress < maxRareTime) return PattyTempState.Rare;
        if (sideProgress < maxMediumTime) return PattyTempState.Medium;
        if (sideProgress < maxWellTime) return PattyTempState.Well;
        return PattyTempState.Burnt;
    }

    //When clicked on grill, flip it!
    private void Flip()
    {
        if (!flipping && isOnGrill) StartCoroutine(FlipRoutine());
    }

    //Show flip animation and change side of patty being cooked
    private IEnumerator FlipRoutine()
    {
        flipping = true;
        flipped = !flipped;

        Quaternion startRot = rb.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(180f, 0f, 0f);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * flipSpeed;
            rb.MoveRotation(Quaternion.Slerp(startRot, endRot, t));
            yield return null;
        }

        rb.MoveRotation(endRot);
        flipping = false;
    }

    //Get ref to player when being held
    public void SetPickupScript(PlayerPickup playerPickupRef)
    {
        playerPickup = playerPickupRef;
    }
}
