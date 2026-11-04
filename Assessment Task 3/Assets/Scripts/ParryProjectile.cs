using System.Data.Common;
using UnityEngine;

public class ParryProjectile : MonoBehaviour
{
    public float speed = 18f;
    public int damage = 3;
    public float Lifetime = 3f;
    public LayerMask groundLayer;
    Vector2 dir;
    LayerMask enemyLayer;

    public void Launch(Vector2 direction, LayerMask enemies)
    {
        dir = direction.normalized;
        enemyLayer = enemies;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        Destroy(gameObject, Lifetime);
    }

    void Update()
    {
        transform.position += (Vector3)(dir * speed * Time.deltaTime);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        int bit = 1 << other.gameObject.layer;

        if ((enemyLayer.value & bit) != 0)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;
            target.TakeDamage(damage, transform.position);
            Destroy(gameObject);
        }
        else if ((groundLayer.value & bit) != 0)
        {
            Destroy(gameObject);
        }
    }
}
// 	l'p orvlqj pb plqg, lp qrw rndb, li brx'yh uhdg wklv sohdvh wdon wr ph.. - pradhyot.N 28/10/2026
// (shift 3)