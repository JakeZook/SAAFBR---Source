using Unity.Mathematics;
using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 3f;
    public Transform holdPoint;
    public Camera playerCamera;

    [Header("Spring Settings")]
    public float springStrength = 500f;
    public float springDamping = 50f;
    public float rotationLerpSpeed = 10f;

    [Header("Throw Settings")]
    public float minThrowForce = 3f;
    public float maxThrowForce = 6f;
    public float maxChargeTime = 2f;

    [Header("Charge Shake")]
    public float shakeAmount = 2f;  
    public float shakeSpeed = 250f;  

    private GameObject heldObject;
    private Rigidbody heldRb;
    private Collider heldCol;
    private GameObject highlightedObject;

    private Quaternion rotationOffset;
    private bool isChargingThrow = false;
    private float throwChargeTime = 0f;

    private void OnEnable()
    {
        TestCustomer.dropOffOrder += DropObject;
    }

    private void OnDisable()
    {
        TestCustomer.dropOffOrder -= DropObject;
    }

    void Update()
    {
        HandleHighlight();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject == null && highlightedObject != null)
            {
                //Needs this to be able to drop patty when colliding with grill
                Patty patty = highlightedObject.GetComponent<Patty>();
                if (patty != null)
                {
                    patty.SetPickupScript(this);
                }

                //Needs this to be able to drop bacon when colliding with grill
                Bacon bacon = highlightedObject.GetComponent<Bacon>();
                if (bacon != null)
                {
                    bacon.SetPickupScript(this);
                }

                //Needs this to take clean plate off sink
                Sink sink = highlightedObject.GetComponent<Sink>();
                if (sink != null)
                {
                    GameObject plate = sink.PickUpCLeanDish();
                    if (plate) highlightedObject = plate;
                    else return;
                }

                //Pickup highlighted object
                if (highlightedObject.CompareTag("Pickup") || highlightedObject.CompareTag("Useable")) PickupObject(highlightedObject); 
            }
            else if (heldObject != null)
            {
                DropObject();
            }
        }

        if (heldObject != null)
        {
            IUseable useable = heldObject.GetComponent<IUseable>();
            if (useable != null)
            {
                if (Input.GetMouseButton(0))
                {
                    useable.Use();
                    return;
                }
            }
            
            if (Input.GetMouseButtonDown(0))
            {
                isChargingThrow = true;
                throwChargeTime = 0f;
            }

            if (Input.GetMouseButton(0) && isChargingThrow)
            {
                throwChargeTime += Time.deltaTime;
                throwChargeTime = Mathf.Min(throwChargeTime, maxChargeTime);
            }

            if (Input.GetMouseButtonUp(0) && isChargingThrow)
            {
                ThrowObject();
                isChargingThrow = false;
            }
        }

        if (Input.GetMouseButtonDown(0) && highlightedObject != null)
        {
            IInteractable interactable = highlightedObject.GetComponent<IInteractable>();
            if (interactable != null) interactable.Interact();
        }
    }

    void FixedUpdate()
    {
        if (heldObject != null && heldRb != null)
        {
            Vector3 targetPosition = holdPoint.position;

            IUseable useable = heldObject.GetComponent<IUseable>();

            if (useable != null)
            {
                targetPosition += holdPoint.TransformDirection(useable.GetTransformOffset());
            }

            Vector3 toHoldPoint = targetPosition - heldObject.transform.position;

            Vector3 force = (toHoldPoint * springStrength) - (heldRb.velocity * springDamping);

            heldRb.AddForce(force, ForceMode.Acceleration);

            Quaternion flatHoldRotation = GetFlatYawRotation(holdPoint.rotation);
            Quaternion targetRotation = flatHoldRotation * rotationOffset;

            // Apply shake if charging
            if (isChargingThrow)
            {
                float shakeX = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount * (throwChargeTime / maxChargeTime);
                float shakeY = Mathf.Cos(Time.time * shakeSpeed) * shakeAmount * (throwChargeTime / maxChargeTime);
                targetRotation *= Quaternion.Euler(shakeX * 100f, shakeY * 100f, 0f);
            }

            float currentLerpSpeed = isChargingThrow ? rotationLerpSpeed * 0.2f : rotationLerpSpeed;

            // Exponential smoothing instead of a raw Lerp with an uncapped t value
            float rotT = 1f - Mathf.Exp(-currentLerpSpeed * Time.fixedDeltaTime);
            Quaternion smoothedRotation = Quaternion.Lerp(heldObject.transform.rotation, targetRotation, rotT);

            heldRb.MoveRotation(smoothedRotation);
        }
    }

    Quaternion GetFlatYawRotation(Quaternion sourceRotation)
    {
        Vector3 flatForward = sourceRotation * Vector3.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = sourceRotation * Vector3.up;
        flatForward.Normalize();
        return Quaternion.LookRotation(flatForward, Vector3.up);
    }

    public void PickupObject(GameObject obj)
    {
        heldObject = obj;
        heldRb = heldObject.GetComponent<Rigidbody>();
        heldCol = heldObject.GetComponent<Collider>();

        if (heldRb != null)
        {
            heldRb.useGravity = false;
            heldRb.drag = 0f;
            heldRb.angularDrag = 5f;
            heldRb.interpolation = RigidbodyInterpolation.Interpolate;
            heldRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (heldObject.GetComponent<BurgerStack>() != null)
                heldRb.constraints = RigidbodyConstraints.FreezeRotation;
            else
                heldRb.constraints = RigidbodyConstraints.None;
        }

        Quaternion desiredRotation = Quaternion.Euler(-90f, 0f, 0f);
        IUseable useable = heldObject.GetComponent<IUseable>();
        if (useable != null) desiredRotation = useable.GetRotationOffset();

        // No more pickup-time calibration — just store the offset itself
        rotationOffset = desiredRotation;

        ClearHighlight();
    }

    public void DropObject()
    {
        if (heldRb != null)
        {
            heldRb.useGravity = true;
            heldRb.drag = 0f;
            heldRb.angularDrag = 0.05f;
        }

        heldObject = null;
        heldRb = null;
        heldCol = null;
    }

    void ThrowObject()
    {
        if (heldRb == null) return;

        float chargePercent = throwChargeTime / maxChargeTime;
        float force = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);

        //If a plate or burger stack, reset rotation so it is upright
        if (heldObject.GetComponent<BurgerStack>() != null) heldObject.transform.rotation = Quaternion.Euler(-90,0,0);

        heldRb.useGravity = true;
        heldRb.drag = 0f;
        heldRb.angularDrag = 0.05f;

        heldRb.AddForce(playerCamera.transform.forward * force, ForceMode.VelocityChange);

        heldObject = null;
        heldRb = null;
        heldCol = null;
    }

    void HandleHighlight()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Pickup") || hit.collider.CompareTag("Interactable") || hit.collider.CompareTag("Useable"))
            {
                if (hit.collider.gameObject != heldObject)
                {
                    if (highlightedObject != hit.collider.gameObject)
                    {
                        ClearHighlight();
                        highlightedObject = hit.collider.gameObject;
                        SetOutline(highlightedObject, true);
                    }
                    return;
                }
            }
        }
        ClearHighlight();
    }

    void SetOutline(GameObject obj, bool state)
    {
        Outline outline = obj.GetComponent<Outline>();
        if (outline != null) outline.enabled = state;
    }

    void ClearHighlight()
    {
        if (highlightedObject != null)
        {
            SetOutline(highlightedObject, false);
            highlightedObject = null;
        }
    }
}
