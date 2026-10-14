using UnityEngine;

[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 6f;
    [SerializeField] private float impactVfxLifeTime = 2.5f;
    public float damage = 10f;
    public bool playerOnly;

    public GameObject impactVfx;
    public float impactVfxScale = 1f;

    [Tooltip("Distancia horizontal desde la boca a la que empieza a caer (gravedad).")]
    public float fallStartDistance = 45f;
    [Tooltip("Distancia horizontal total tras la cual explota (se apaga).")]
    public float maxRange = 60f;

    private bool consumed;
    private Vector3 startPos;
    private Rigidbody body;
    private bool falling;

    private void Start()
    {
        startPos = transform.position;
        body = GetComponent<Rigidbody>();
        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        if (consumed)
        {
            return;
        }

        Vector3 delta = transform.position - startPos;
        delta.y = 0f;
        float traveled = delta.magnitude;

        if (!falling && fallStartDistance > 0f && traveled >= fallStartDistance)
        {
            falling = true;
            if (body != null)
            {
                body.useGravity = true;
            }
        }

        if (maxRange > 0f && traveled >= maxRange)
        {
            Impact(null, transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 point = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
        Impact(collision.collider, point);
    }

    private void OnTriggerEnter(Collider other)
    {
        Impact(other, transform.position);
    }

    private void Impact(Collider collider, Vector3 point)
    {
        if (consumed)
        {
            return;
        }
        consumed = true;

        if (collider != null)
        {
            ApplyDamage(collider);
        }

        if (impactVfx != null)
        {
            GameObject vfx = Instantiate(impactVfx, point, Quaternion.identity);
            vfx.transform.localScale *= impactVfxScale;
            Destroy(vfx, impactVfxLifeTime);
        }
        Destroy(gameObject);
    }

    private void ApplyDamage(Collider collider)
    {
        if (playerOnly && collider.GetComponentInParent<TankController>() == null)
        {
            return;
        }

        IDamagable damagable = collider.GetComponent<IDamagable>();
        if (damagable == null)
        {
            damagable = collider.GetComponentInParent<IDamagable>();
        }
        damagable?.TakeDamage(damage);
    }
}
