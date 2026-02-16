using UnityEngine;

/// <summary>
/// Bullet that the player shoots.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : Spawnable
{
    /// <summary>
    /// Sent when an incoming collider makes contact with this object's collider (2D physics only). This function can be a coroutine.
    /// </summary>
    /// <param name="_">The Collision2D data associated with this collision.</param>
    private void OnCollisionEnter2D(Collision2D _)
    {
        // A collision is only possible with an asteroid so set that it has.
        player.DestroyedAsteroid();
        
        // Destroy the bullet.
        Destroy(gameObject);
    }
}