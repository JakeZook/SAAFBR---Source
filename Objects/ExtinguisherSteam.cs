using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtinguisherSteam : MonoBehaviour
{
    private ParticleSystem particles;
    private CapsuleCollider capsule;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        particles.Stop();
        capsule = GetComponent<CapsuleCollider>();
    }

    public void StartSteam()
    {
        particles.Play();
        capsule.enabled = true;
    }

    public void StopSteam()
    {
        particles.Stop();
        capsule.enabled = false;
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("Fire"))
        {
            Grill grill = collision.GetComponentInParent<Grill>();
            if (!grill) return;

            grill.PutOutFire();
        }
    }
}
