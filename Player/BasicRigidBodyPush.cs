using System.Collections.Generic;
using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
    public LayerMask pushLayers;
    public bool canPush = true;

    [Header("Push Settings")]
    [Range(0.5f, 10f)]
    public float strength = 1.1f;

    // OnControllerColliderHit fires during CharacterController.Move() in
    // Update(), not in FixedUpdate() where Rigidbody physics is actually
    // simulated. Calling AddForce directly from the hit callback means it
    // can fire more than once per physics step (or be skipped entirely,
    // depending on frame rate), stacking or dropping impulses unevenly -
    // that mismatch is what causes the jitter.
    //
    // Instead, we just record which body to push and in which direction,
    // then apply it once per body in FixedUpdate, in sync with the physics
    // step.
    private struct PendingPush
    {
        public Rigidbody body;
        public Vector3 direction;
    }

    private readonly List<PendingPush> _pendingPushes = new List<PendingPush>();

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!canPush)
            return;

        QueuePush(hit);
    }

    private void QueuePush(ControllerColliderHit hit)
    {
        // Get the Rigidbody we hit.
        Rigidbody body = hit.collider.attachedRigidbody;

        // No Rigidbody or Rigidbody is kinematic.
        if (body == null || body.isKinematic)
            return;

        // Check if the object is on a pushable layer.
        if ((pushLayers.value & (1 << body.gameObject.layer)) == 0)
            return;

        // Don't push objects when landing on top of them.
        if (hit.moveDirection.y < -0.3f)
            return;

        // Get the direction the player is moving.
        Vector3 pushDirection = hit.moveDirection;

        // Only push horizontally.
        pushDirection.y = 0f;

        // Make sure we have a valid direction.
        if (pushDirection.sqrMagnitude < 0.001f)
            return;

        pushDirection.Normalize();

        // If this body is already queued this physics step, just update its
        // direction instead of adding a second entry - keeps it to one push
        // per body per FixedUpdate no matter how many hits come in.
        for (int i = 0; i < _pendingPushes.Count; i++)
        {
            if (_pendingPushes[i].body == body)
            {
                _pendingPushes[i] = new PendingPush { body = body, direction = pushDirection };
                return;
            }
        }

        _pendingPushes.Add(new PendingPush { body = body, direction = pushDirection });
    }

    private void FixedUpdate()
    {
        if (_pendingPushes.Count == 0)
            return;

        foreach (PendingPush push in _pendingPushes)
        {
            if (push.body == null)
                continue;

            // Rather than adding a fresh burst of momentum every physics step
            // (which either stacks into runaway jitter if applied too often,
            // or feels like nothing happens if throttled to one small impulse
            // per step), push the body's horizontal velocity up to a target
            // speed ("strength") along the push direction. This gives an
            // immediate, consistent push and naturally stops adding once the
            // object is already moving that fast - no stacking, no dead zone.
            Vector3 horizontalVelocity = new Vector3(push.body.velocity.x, 0f, push.body.velocity.z);
            float currentSpeedAlongPush = Vector3.Dot(horizontalVelocity, push.direction);

            if (currentSpeedAlongPush < strength)
            {
                Vector3 velocityToAdd = push.direction * (strength - currentSpeedAlongPush);
                push.body.AddForce(velocityToAdd, ForceMode.VelocityChange);
            }
        }

        _pendingPushes.Clear();
    }
}