using UnityEngine;

/// <summary>
/// Base spawnable class for bullets and asteroids.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Spawnable : MonoBehaviour
{
    /// <summary>
    /// The rigidbody for movement.
    /// </summary>
    [Header("Base Requirements")]
    [Tooltip("The rigidbody for movement.")]
    [SerializeField]
    private Rigidbody2D body;
    
    /// <summary>
    /// How much force to add.
    /// </summary>
    [field: Tooltip("How much force to add.")]
    [field: Min(float.Epsilon)]
    [field: SerializeField]
    public float Speed { get; private set; } = 1;
    
    /// <summary>
    /// Reference to the player.
    /// </summary>
    protected Player Player;
    
    /// <summary>
    /// Initialize the object.
    /// </summary>
    /// <param name="direction">The direction to move in.</param>
    /// <param name="p">The player that shot the bullet.</param>
    public void Initialize(Vector2 direction, Player p)
    {
        // Keep a reference to the player.
        Player = p;
        Player.Spawned.Add(gameObject);
        
        // Add force once since there is no drag.
        body.linearVelocity = direction * Speed;
    }
    
    /// <summary>
    /// Destroying the attached Behaviour will result in the game or Scene receiving OnDestroy.
    /// </summary>
    private void OnDestroy()
    {
        // Clean up the reference.
        Player.Spawned.Remove(gameObject);
    }

    private void Update()
    {
        //Debug.Log(body.linearVelocity.magnitude);
    }
}