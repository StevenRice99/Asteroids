using UnityEngine;

/// <summary>
/// Bullet that the player shoots.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : Spawnable
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // A collision is only possible with an asteroid so set that it has.
        player.DestroyedAsteroid();
        
        // Destroy the bullet.
        Destroy(gameObject);
    }
}