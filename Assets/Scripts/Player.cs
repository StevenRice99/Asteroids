using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : Agent
{
    private enum Turn
    {
        None,
        Left,
        Right
    }
    
    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");

    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private static readonly int Cull = Shader.PropertyToID("_Cull");

    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float asteroidScore = 2;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float positionAward = 1;

    [Min(float.Epsilon)]
    [SerializeField]
    private float movePenalty = 0.1f;

    [Min(float.Epsilon)]
    [SerializeField]
    private float turnPenalty = 0.01f;

    [Min(float.Epsilon)]
    [SerializeField]
    private float shootPenalty = 1;
    
    [SerializeField]
    private Asteroid asteroidPrefab;

    [Min(float.Epsilon)]
    [SerializeField]
    private float levelSize = 5;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float spawnPadding = 5;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float spawnRate = 2;
    
    [Range(0f, 45f)]
    [SerializeField]
    private float trajectoryVariance = 15;
    
    [SerializeField]
    private Bullet bulletPrefab;
    
    [SerializeField]
    private Rigidbody2D body;

    [Min(float.Epsilon)]
    [SerializeField]
    private float moveSpeed = 1;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float turnSpeed = 0.1f;

    [Min(float.Epsilon)]
    [SerializeField]
    private float shootDelay = 0.2f;
    
    private bool _move;

    private Turn _turn;

    private bool _shoot;

    private bool _canShoot = true;

    private float _elapsedTime;

    public override void OnEpisodeBegin()
    {
        foreach (Asteroid a in FindObjectsOfType<Asteroid>())
        {
            Destroy(a.gameObject);
        }

        foreach (Bullet b in FindObjectsOfType<Bullet>())
        {
            Destroy(b.gameObject);
        }

        Transform t = transform;
        t.position = Vector3.zero;
        t.rotation = Quaternion.identity;
        
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;

        _elapsedTime = 0;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.W) ? 1 : 0;
        discreteActions[1] = (int) (Input.GetKey(KeyCode.A) ? Turn.Left : Input.GetKey(KeyCode.D) ? Turn.Right : Turn.None);
        discreteActions[2] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 p = transform.localPosition;
        sensor.AddObservation((p.x / levelSize + 1) / 2);
        sensor.AddObservation((p.y / levelSize + 1) / 2);
        sensor.AddObservation(transform.localEulerAngles.z / 360);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        _move = actions.DiscreteActions[0] > 0;
        _turn = (Turn) actions.DiscreteActions[1];
        _shoot = actions.DiscreteActions[2] > 0;
    }

    private void FixedUpdate()
    {
        Transform t = transform;
        Vector3 p = t.position;

        if (p.x <= -levelSize || p.x >= levelSize || p.y <= -levelSize || p.y >= levelSize)
        {
            EndEpisode();
            return;
        }
        
        float deltaTime = Time.fixedDeltaTime;

        _elapsedTime += deltaTime;
        
        if (_elapsedTime >= spawnRate)
        {
            // Choose a random direction from the center of the spawner and
            // spawn the asteroid a distance away
            Vector2 spawnDirection = Random.insideUnitCircle.normalized;
            Vector3 spawnPoint = spawnDirection * (levelSize +spawnPadding);

            // Calculate a random variance in the asteroid's rotation which will
            // cause its trajectory to change
            float variance = Random.Range(-trajectoryVariance, trajectoryVariance);
            Quaternion rotation = Quaternion.AngleAxis(variance, Vector3.forward);

            // Create the new asteroid by cloning the prefab and set a random
            // size within the range
            Asteroid asteroid = Instantiate(asteroidPrefab, spawnPoint, rotation);
            asteroid.size = Random.Range(asteroid.minSize, asteroid.maxSize);

            // Set the trajectory to move in the direction of the spawner
            Vector2 trajectory = rotation * -spawnDirection;
            asteroid.SetTrajectory(trajectory);

            _elapsedTime = 0;
        }

        AddReward(Mathf.Max(0, levelSize - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y))) / levelSize * positionAward * deltaTime);
        
        RequestDecision();
        
        if (_move)
        {
            body.AddForce(t.up * moveSpeed);
            AddReward(-movePenalty * deltaTime);
        }
        if (_turn != Turn.None)
        {
            body.AddTorque(turnSpeed * (_turn == Turn.Left ? 1 : -1));
            AddReward(-turnPenalty * deltaTime);
        }

        if (!_canShoot || !_shoot)
        {
            return;
        }
        
        AddReward(-shootPenalty);
        Bullet bullet = Instantiate(bulletPrefab, t.position, t.rotation);
        bullet.Project(t.up, this);
        
        StopAllCoroutines();
        StartCoroutine(ShootCooldown());
        
        IEnumerator ShootCooldown()
        {
            _canShoot = false;
            yield return new WaitForSeconds(shootDelay);
            _canShoot = true;
        }
    }

    public void DestroyedAsteroid()
    {
        AddReward(asteroidScore);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EndEpisode();
    }

    private Material _lineMaterial;

    private void OnRenderObject()
    {
        if (_lineMaterial == null)
        {
            // Unity has a built-in shader that is useful for drawing simple colored things.
            _lineMaterial = new(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            
            // Turn on alpha blending.
            _lineMaterial.SetInt(SrcBlend, (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt(DstBlend, (int)BlendMode.OneMinusSrcAlpha);
            
            // Turn backface culling off.
            _lineMaterial.SetInt(Cull, (int)CullMode.Off);
            
            // Turn off depth writes.
            _lineMaterial.SetInt(ZWrite, 0);
        }

        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        
        GL.Color(Color.green);

        Vector3 scale = transform.localScale;
        
        GL.Vertex(new(-levelSize - scale.x / 2, levelSize + scale.y / 2, 0));
        GL.Vertex(new(levelSize + scale.x / 2, levelSize + scale.y / 2, 0));
        
        GL.Vertex(new(levelSize + scale.x / 2, levelSize + scale.y / 2, 0));
        GL.Vertex(new(levelSize + scale.x / 2, -levelSize - scale.y / 2, 0));
        
        GL.Vertex(new(levelSize + scale.x / 2, -levelSize - scale.y / 2, 0));
        GL.Vertex(new(-levelSize - scale.x / 2, -levelSize - scale.y / 2, 0));
        
        GL.Vertex(new(-levelSize - scale.x / 2, -levelSize - scale.y / 2, 0));
        GL.Vertex(new(-levelSize - scale.x / 2, levelSize + scale.y / 2, 0));
        
        GL.End();
        GL.PopMatrix();
    }
}