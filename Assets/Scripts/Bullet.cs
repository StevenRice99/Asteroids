using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [SerializeField]
    private Rigidbody2D body;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float speed = 500;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float maxLifetime = 10;

    private Player _player;

    public void Project(Vector2 direction, Player player)
    {
        _player = player;
        
        // The bullet only needs a force to be added once since they have no
        // drag to make them stop moving
        body.AddForce(direction * speed);

        // Destroy the bullet after it reaches it max lifetime
        Destroy(gameObject, maxLifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        _player.DestroyedAsteroid();
        Destroy(gameObject);
    }
}