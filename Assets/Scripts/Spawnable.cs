using UnityEngine;

/// <summary>
/// Base spawnable class for bullets and asteroids.
/// </summary>
public abstract class Spawnable : MonoBehaviour
{
    [Header("Base Requirements")]
    [Tooltip("The rigidbody for movement.")]
    [SerializeField]
    private Rigidbody2D body;
    
    [Tooltip("How much force to add.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float speed = 50;
    
    /// <summary>
    /// Reference to the player.
    /// </summary>
    protected Player player;
    
    /// <summary>
    /// Initialize the object.
    /// </summary>
    /// <param name="direction">The direction to move in.</param>
    /// <param name="p">The player that shot the bullet.</param>
    public void Initialize(Vector2 direction, Player p)
    {
        // Keep a reference to the player.
        player = p;
        player.Spawned.Add(gameObject);
        
        // Add force once since there is no drag.
        body.AddForce(direction * speed);
    }

    private void OnDestroy()
    {
        // Clean up the reference.
        player.Spawned.Remove(gameObject);
    }
}