using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Asteroid : MonoBehaviour
{
    [SerializeField]
    private Rigidbody2D body;
    
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    
    [SerializeField]
    private Sprite[] sprites;

    [Min(float.Epsilon)]
    public float size = 1;
    
    [Min(float.Epsilon)]
    public float minSize = 0.35f;
    
    [Min(float.Epsilon)]
    public float maxSize = 1.65f;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float movementSpeed = 50;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float maxLifetime = 30;

    private void Start()
    {
        // Assign random properties to make each asteroid feel unique
        spriteRenderer.sprite = sprites[Random.Range(0, sprites.Length)];
        Transform t = transform;
        t.eulerAngles = new Vector3(0f, 0f, Random.value * 360f);

        // Set the scale and mass of the asteroid based on the assigned size so
        // the physics is more realistic
        t.localScale = Vector3.one * size;
        body.mass = size;

        // Destroy the asteroid after it reaches its max lifetime
        Destroy(gameObject, maxLifetime);
    }

    public void SetTrajectory(Vector2 direction)
    {
        // The asteroid only needs a force to be added once since they have no
        // drag to make them stop moving
        body.AddForce(direction * movementSpeed);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the asteroid is large enough to split in half
        // (both parts must be greater than the minimum size)
        if (size * 0.5f >= minSize)
        {
            CreateSplit();
            CreateSplit();
        }

        // Destroy the current asteroid since it is either replaced by two
        // new asteroids or small enough to be destroyed by the bullet
        Destroy(gameObject);
        
        void CreateSplit()
        {
            // Set the new asteroid position to be the same as the current asteroid
            // but with a slight offset so they do not spawn inside each other
            Transform t = transform;
            Vector2 position = t.position;
            position += Random.insideUnitCircle * 0.5f;

            // Create the new asteroid at half the size of the current
            Asteroid half = Instantiate(this, position, t.rotation);
            half.size = size * 0.5f;

            // Set a random trajectory
            half.SetTrajectory(Random.insideUnitCircle.normalized);
        }
    }
}