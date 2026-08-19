using UnityEngine;

public class SauceBottle : MonoBehaviour, IUseable
{
    [SerializeField] Quaternion rotationOffset = Quaternion.Euler(90f, 0f, 0f);
    [SerializeField] Vector3 transformOffset = new Vector3(0f, .2f, 0f);
    [SerializeField] IngredientSO sauce;

    [Header("Spray Settings")]
    [SerializeField] private float sprayDuration = 0.2f;

    private ParticleSystem particles;
    private BoxCollider col;

    private bool isBeingUsed = false;
    private float sprayTimer = 0f;

    private void Start()
    {
        particles = GetComponentInChildren<ParticleSystem>();
        col = transform.GetChild(1).GetComponent<BoxCollider>();

        col.enabled = false;
        particles.Stop();
    }

    private void Update()
    {
        if (sprayTimer > 0f)sprayTimer -= Time.deltaTime;

        else if (!isBeingUsed) StopParticles();
        isBeingUsed = false;
    }

    public void Use()
    {
        isBeingUsed = true;

        sprayTimer = sprayDuration;

        if (!particles.isPlaying) particles.Play();
        col.enabled = true;
    }

    private void StopParticles()
    {
        if (particles.isPlaying) particles.Stop();
        col.enabled = false;
    }

    public Quaternion GetRotationOffset()
    {
        return rotationOffset;
    }

    public Vector3 GetTransformOffset()
    {
        return transformOffset;
    }
}