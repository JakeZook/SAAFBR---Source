using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 3f;
    public Transform holdPoint;
    public Transform usePoint;
    [Tooltip("Where the held object moves to while charging a throw (e.g. positioned to the side, like a wind-up). Falls back to holdPoint if left empty.")]
    public Transform throwPoint;
    public Camera playerCamera;

    [Header("Spring Settings")]
    public float springStrength = 500f;
    public float springDamping = 50f;
    public float rotationLerpSpeed = 10f;

    [Header("Throw Settings")]
    public float minThrowForce = 3f;
    public float maxThrowForce = 6f;
    public float maxChargeTime = 2f;
    [Tooltip("Maps charge progress (0-1, x-axis) to how much of the min-to-max force range is applied (0-1, y-axis). " +
        "Raise the curve's starting y value so a tap doesn't feel weak, and flatten the early slope so small charge " +
        "differences don't swing the force as much.")]
    public AnimationCurve throwForceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Charge Shake")]
    public float shakeAmount = 2f;  
    public float shakeSpeed = 250f;  

    [Header("Trajectory Prediction")]
    public LineRenderer trajectoryLine;
    [Tooltip("Number of points sampled along the arc. Higher = smoother line.")]
    public int trajectoryResolution = 30;
    [Tooltip("Simulated time between each sampled point (seconds).")]
    public float trajectoryTimeStep = 0.05f;
    [Tooltip("What the trajectory line stops at when it would hit something.")]
    public LayerMask trajectoryCollisionMask = ~0;
    [Tooltip("Optional marker (e.g. a flat disc/reticle) shown at the predicted landing point. Leave empty to skip.")]
    public Transform trajectoryEndMarker;
    [Tooltip("How far the marker sits off the surface it lands on, to avoid z-fighting.")]
    public float trajectoryEndMarkerOffset = 0.02f;
    [Tooltip("Distance along the throw direction the line starts from, so it clears the held object instead of starting inside/behind it.")]
    public float trajectoryStartOffset = 0.5f;
    [SerializeField] TrajectoryMarker trajectoryMarker;

    public GameObject heldObject;
    private Rigidbody heldRb;
    private Collider heldCol;
    private GameObject highlightedObject;

    private Quaternion rotationOffset;
    private bool isChargingThrow = false;
    private float throwChargeTime = 0f;

    private void OnEnable()
    {
        Customer.dropOffOrder += DropObject;
    }

    private void OnDisable()
    {
        Customer.dropOffOrder -= DropObject;
    }

    private void Awake()
    {
        if (trajectoryLine != null)
        {
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.enabled = false;
        }

        if (trajectoryEndMarker != null) trajectoryEndMarker.gameObject.SetActive(false);
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

            if (isChargingThrow)
            {
                UpdateTrajectoryLine();
            }

            if (Input.GetMouseButtonUp(0) && isChargingThrow)
            {
                ThrowObject();
                isChargingThrow = false;
                HideTrajectoryLine();
            }
        }
        else
        {
            HideTrajectoryLine();
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
            IUseable useable = heldObject.GetComponent<IUseable>();
            Transform anchor;
            if (useable != null && usePoint != null) anchor = usePoint;
            else if (isChargingThrow && throwPoint != null) anchor = throwPoint;
            else anchor = holdPoint;

            Vector3 targetPosition = anchor.position;

            if (useable != null)
            {
                targetPosition += anchor.TransformDirection(useable.GetTransformOffset());
            }

            Vector3 toHoldPoint = targetPosition - heldObject.transform.position;

            Vector3 force = (toHoldPoint * springStrength) - (heldRb.velocity * springDamping);

            heldRb.AddForce(force, ForceMode.Acceleration);

            Quaternion flatHoldRotation = GetFlatYawRotation(anchor.rotation);
            Quaternion targetRotation = flatHoldRotation * rotationOffset;

            // Apply shake if charging
            if (isChargingThrow)
            {
                float shakeX = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount * (throwChargeTime / maxChargeTime);
                float shakeY = Mathf.Cos(Time.time * shakeSpeed) * shakeAmount * (throwChargeTime / maxChargeTime);
                targetRotation *= Quaternion.Euler(shakeX * 100f, shakeY * 100f, 0f);
            }

            float currentLerpSpeed = isChargingThrow ? rotationLerpSpeed * 0.2f : rotationLerpSpeed;

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
            heldRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (heldObject.GetComponent<BurgerStack>() != null)
                heldRb.constraints = RigidbodyConstraints.FreezeRotation;
            else
                heldRb.constraints = RigidbodyConstraints.None;
        }

        Quaternion desiredRotation = Quaternion.Euler(-90f, 0f, 0f);
        IUseable useable = heldObject.GetComponent<IUseable>();
        if (useable != null) desiredRotation = useable.GetRotationOffset();

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

        isChargingThrow = false;
        HideTrajectoryLine();
    }

    void ThrowObject()
    {
        if (heldRb == null) return;

        float force = GetCurrentThrowForce();

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

    float GetCurrentThrowForce()
    {
        float chargePercent = throwChargeTime / maxChargeTime;
        float curvedPercent = throwForceCurve.Evaluate(chargePercent);
        return Mathf.Lerp(minThrowForce, maxThrowForce, curvedPercent);
    }

    void UpdateTrajectoryLine()
    {
        if (trajectoryLine == null || heldObject == null) return;

        float force = GetCurrentThrowForce();
        Vector3 velocity = playerCamera.transform.forward * force;

        Transform lineOrigin = throwPoint != null ? throwPoint : holdPoint;
        Vector3 origin = lineOrigin.position;
        Vector3 point = origin;

        var visiblePoints = new List<Vector3>(trajectoryResolution);

        for (int i = 0; i < trajectoryResolution; i++)
        {
            if (Vector3.Distance(origin, point) >= trajectoryStartOffset)
            {
                visiblePoints.Add(point);
            }

            Vector3 nextPoint = point + velocity * trajectoryTimeStep + 0.5f * Physics.gravity * trajectoryTimeStep * trajectoryTimeStep;

            if (Physics.Linecast(point, nextPoint, out RaycastHit hit, trajectoryCollisionMask, QueryTriggerInteraction.Ignore))
            {
                if (visiblePoints.Count == 0) visiblePoints.Add(origin);
                visiblePoints.Add(hit.point);
                DrawLine(visiblePoints);
                ShowEndMarker(hit);
                return;
            }

            velocity += Physics.gravity * trajectoryTimeStep;
            point = nextPoint;
        }

        // there's no landing point to mark yet.
        DrawLine(visiblePoints);
        HideEndMarker();
    }

    void DrawLine(List<Vector3> points)
    {
        trajectoryLine.enabled = true;
        trajectoryLine.positionCount = points.Count;
        trajectoryLine.SetPositions(points.ToArray());
    }

    void ShowEndMarker(RaycastHit hit)
    {
        if (trajectoryEndMarker == null) return;

        trajectoryEndMarker.gameObject.SetActive(true);
        trajectoryEndMarker.position = hit.point + hit.normal * trajectoryEndMarkerOffset;
        trajectoryEndMarker.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
    }

    void HideEndMarker()
    {
        if (trajectoryEndMarker != null)
        {
            trajectoryEndMarker.gameObject.SetActive(false);
            trajectoryMarker.RemoveOutline();
        }
    }

    void HideTrajectoryLine()
    {
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        HideEndMarker();
    }

    void HandleHighlight()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, ~0, QueryTriggerInteraction.Ignore))
        {
            bool validTag = hit.collider.CompareTag("Pickup") || hit.collider.CompareTag("Interactable") || hit.collider.CompareTag("Useable");

            if (validTag)
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                bool canHighlight = interactable == null || interactable.CanHighlight();

                if (canHighlight && hit.collider.gameObject != heldObject)
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