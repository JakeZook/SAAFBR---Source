using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
    public LayerMask pushLayers;
    public bool canPush = true;

    [Header("Push Settings")]
    [Range(0.5f, 10f)]
    public float strength = 1.1f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!canPush)
            return;

        PushRigidBody(hit);
    }

    private void PushRigidBody(ControllerColliderHit hit)
    {
        // Get the Rigidbody we hit
        Rigidbody body = hit.collider.attachedRigidbody;

        // No Rigidbody or Rigidbody is kinematic
        if (body == null || body.isKinematic)
            return;

        // Check if the object is on a pushable layer
        if ((pushLayers.value & (1 << body.gameObject.layer)) == 0)
            return;

        // Don't push objects when landing on top of them
        if (hit.moveDirection.y < -0.3f)
            return;

        // Get the direction the player is moving
        Vector3 pushDirection = hit.moveDirection;

        // Only push horizontally
        pushDirection.y = 0f;

        // Make sure we have a valid direction
        if (pushDirection.sqrMagnitude < 0.001f)
            return;

        pushDirection.Normalize();

        // Push the Rigidbody
        body.AddForce(
            pushDirection * strength,
            ForceMode.Impulse
        );
    }
}