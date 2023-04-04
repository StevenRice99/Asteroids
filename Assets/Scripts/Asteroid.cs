using UnityEngine;

/// <summary>
/// Handle an asteroid.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Asteroid : Spawnable
{
    [Header("Requirements")]
    [Tooltip("The sprite renderer to apply an option to.")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    
    [Tooltip("The sprite options for the asteroid.")]
    [SerializeField]
    private Sprite[] sprites;

    [Header("Properties")]
    [Tooltip("The ranges for sizes of an asteroid.")]
    public Vector2 sizes = new(0.35f, 1.65f);

    [HideInInspector]
    public float size = 1;

    private void Start()
    {
        // Randomize asteroids.
        spriteRenderer.sprite = sprites[Random.Range(0, sprites.Length)];
        Transform t = transform;
        t.eulerAngles = new(0f, 0f, Random.value * 360f);
        t.localScale = Vector3.one * size;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If large enough, split in half.
        if (size * 0.5f >= sizes.x)
        {
            CreateSplit();
            CreateSplit();
        }

        // Destroy this asteroid.
        Destroy(gameObject);
        
        void CreateSplit()
        {
            // Spawn new at parent location.
            Transform t = transform;
            Vector2 position = t.position;
            position += Random.insideUnitCircle * 0.5f;

            // Half the size.
            Asteroid half = Instantiate(this, position, t.rotation);
            half.size = size * 0.5f;

            // Start moving the new asteroid.
            half.Initialize(Random.insideUnitCircle.normalized, player);
        }
    }
}