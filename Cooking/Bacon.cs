using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Bacon : MonoBehaviour
{
    public enum BaconState { Raw, Cooking, Cooked, Burnt }
    public BaconState state = BaconState.Raw;
    private BaconState previousState;

    [Header("References")]
    public Rigidbody rb;
    private Renderer rend;
    private PlayerPickup playerPickup;
    private Grill grill;

    [SerializeField] private Material[] cookedMats;
    [SerializeField] private Material[] burntMats;

    [Header("Cooking Settings")]
    private float cookProgress = 0f;
    private float minCookTime = 10f;
    private float maxCookTime = 20f;
    public bool isOnGrill = false;

    [Header("Progress Bar")]
    [SerializeField] private GameObject progressBarPrefab; 
    private GameObject progressBarHolder;
    [SerializeField] private Image bar;
    private Color32 rawColor = new Color32(255, 0, 215, 255);
    private Color32 cookedColor = new Color32(0, 255, 0, 255);
    private Color32 burntColor = new Color32(255, 2, 0, 255);


    [Header("Steam Particle")]
    [SerializeField] private GameObject steamPrefab; 
    private GameObject steamInstance;
    [SerializeField] private Color burntSteamColor = new Color(0f, 0f, 0f, 0.8f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();

        previousState = state;

        if (progressBarPrefab != null)
        {
            //Also spawn cooking progress bar, hide until on grill
            progressBarHolder = Instantiate(progressBarPrefab);
            progressBarHolder.SetActive(false);

            Transform holder = progressBarHolder.transform.Find("Holder");
            if (holder != null)
            {
                bar = holder.Find("Bar")?.GetComponent<Image>();
            }
        }
    }

    private void Update()
    {
        //While on grill, cook!
        if (isOnGrill)
        {
            cookProgress += Time.deltaTime;

            //Update visuals and data each frame
            UpdateState();
            UpdateProgressBars();
        }
    }

    private void LateUpdate()
    {
        // If cooking, increase progress bar visual and face to player
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
                if (state == BaconState.Burnt)
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
            state = BaconState.Burnt;
            cookProgress = 20f;
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

    private void UpdateState()
    {
        //Decide when patty is cooked or burnt
        if (cookProgress >= minCookTime) state = BaconState.Cooked;
        if (cookProgress > maxCookTime) state = BaconState.Burnt;

        //Change texture on model to reflect state
        if (state != previousState)
        {
            UpdateMaterials();
            previousState = state;
        }

        //Turn off steam when burnt
        if (state == BaconState.Burnt)
        {
            ParticleSystem steamParticles = steamInstance.GetComponent<ParticleSystem>();
            if (steamParticles != null)
            {
                var main = steamParticles.main;
                main.startColor = burntSteamColor;
            }
        }

        //Change color of progress bar to show state
        UpdateProgressBarColor(bar, state);
    }

    private void UpdateMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (state == BaconState.Cooked)
        {
            foreach (Renderer rend in renderers)
            {
                if (rend.materials.Length == cookedMats.Length)
                {
                    rend.materials = cookedMats;
                }
            }
        }
        else if (state == BaconState.Burnt)
        {
            foreach (Renderer rend in renderers)
            {
                if (rend.materials.Length == burntMats.Length)
                {
                    rend.materials = burntMats;
                }
            }
        }
    }

    private void UpdateProgressBars()
    {
        //Increase bar while on grill
        if (bar != null) bar.fillAmount = Mathf.Clamp01(cookProgress / maxCookTime);
    }

    private void UpdateProgressBarColor(Image bar, BaconState state)
    {
        //Purple = rare, Green = cooked, Red = burnt
        if (state == BaconState.Raw) bar.color = rawColor;
        else if (state == BaconState.Cooked) bar.color = cookedColor;
        else if (state == BaconState.Burnt) bar.color = burntColor;
    }

    //Get ref to player when being held
    public void SetPickupScript(PlayerPickup playerPickupRef)
    {
        playerPickup = playerPickupRef;
    }
}
