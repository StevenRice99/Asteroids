using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Handle an asteroid.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Asteroid : Spawnable
{
    /// <summary>
    /// The sprite renderer to apply an option to.
    /// </summary>
    [Header("Requirements")]
    [Tooltip("The sprite renderer to apply an option to.")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    
    /// <summary>
    /// The sprite options for the asteroid.
    /// </summary>
    [Tooltip("The sprite options for the asteroid.")]
    [SerializeField]
    private Sprite[] sprites;
    
    /// <summary>
    /// The ranges for sizes of an asteroid.
    /// </summary>
    [Header("Properties")]
    [Tooltip("The ranges for sizes of an asteroid.")]
    public Vector2 sizes = new(0.35f, 1.65f);
    
    /// <summary>
    /// The size of this asteroid.
    /// </summary>
    [NonSerialized]
    public float Size = 1;
    
    /// <summary>
    /// Start is called on the frame when a script is enabled just before any of the Update methods are called the first time. This function can be a coroutine.
    /// </summary>
    private void Start()
    {
        // Randomize the asteroid.
        spriteRenderer.sprite = sprites[Random.Range(0, sprites.Length)];
        Transform t = transform;
        t.eulerAngles = new(0f, 0f, Random.value * 360f);
        t.localScale = Vector3.one * Size;
    }
    
    /// <summary>
    /// Sent when an incoming collider makes contact with this object's collider (2D physics only). This function can be a coroutine.
    /// </summary>
    /// <param name="_">The Collision2D data associated with this collision.</param>
    private void OnCollisionEnter2D(Collision2D _)
    {
        // If large enough, split in half.
        if (Size * 0.5f >= sizes.x)
        {
            CreateSplit();
            CreateSplit();
        }
        
        // Destroy this asteroid.
        Destroy(gameObject);
        return;
        
        void CreateSplit()
        {
            // Spawn new at parent location.
            Transform t = transform;
            Vector2 position = t.position;
            position += Random.insideUnitCircle * 0.5f;
            
            // Half the size.
            Asteroid half = Instantiate(this, position, t.rotation);
            half.name = "Asteroid";
            half.Size = Size * 0.5f;
            
            // Start moving the new asteroid.
            half.Initialize(Random.insideUnitCircle.normalized, player);
        }
    }
}