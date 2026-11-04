using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TurretProjectile : MonoBehaviour
{
    public float lifeTime = 4f;

    Vector2 dir;
    float speed;
    int damage;
    bool major;
    bool canBeParried;
    LayerMask groundLayer;

    public void Launch(Vector2 direction, float spd, int dmg, bool isMajor, bool parryable, LayerMask ground)
    {
        dir = direction.normalized;
        speed = spd;
        damage = dmg;
        major = isMajor;
        canBeParried = parryable;
        groundLayer = ground;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += (Vector3)(dir * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCombat player = other.GetComponentInParent<PlayerCombat>();
        if (player != null)
        {
            if (player.IsDead) return;
            if (canBeParried) player.TakeDamage(damage, transform.position, major);
            else player.TakeProjectileDamage(damage, transform.position, major);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & groundLayer.value) != 0)
            Destroy(gameObject);
    }
}