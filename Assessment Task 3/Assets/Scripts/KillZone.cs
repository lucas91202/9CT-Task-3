using UnityEngine;

public class KillZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCombat player = other.GetComponentInParent<PlayerCombat>();
        if (player != null) player.Kill();
    }
}
