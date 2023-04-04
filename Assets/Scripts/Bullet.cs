using UnityEngine;

/// <summary>
/// Bullet that the player shoots.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Requirements")]
    [Tooltip("The rigidbody for the bullet.")]
    [SerializeField]
    private Rigidbody2D body;
    
    [Header("Properties")]
    [Tooltip("How much force to add to the bullet.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float speed = 500;
    
    [Tooltip("How many seconds to destroy the bullet after.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float duration = 10;

    /// <summary>
    /// Reference to the player to apply rewards to if the bullet destroys an asteroid.
    /// </summary>
    private Player _player;

    /// <summary>
    /// Initialize the bullet.
    /// </summary>
    /// <param name="direction">The direction to shoot in.</param>
    /// <param name="player">The player that shot the bullet.</param>
    public void Initialize(Vector2 direction, Player player)
    {
        // Keep a reference to the player.
        _player = player;
        
        // Add force once since there is no drag.
        body.AddForce(direction * speed);

        // Destroy after its max duration.
        Destroy(gameObject, duration);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // A collision is only possible with an asteroid so set that it has.
        _player.DestroyedAsteroid();
        
        // Destroy the bullet.
        Destroy(gameObject);
    }
}