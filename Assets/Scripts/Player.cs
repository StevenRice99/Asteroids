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
    private float moveCost = 0.25f;

    [Min(float.Epsilon)]
    [SerializeField]
    private float turnCost = 0.1f;

    [Min(float.Epsilon)]
    [SerializeField]
    private float shootCost = 1;

    [Min(float.Epsilon)]
    [SerializeField]
    private float asteroidScore = 10;
    
    [SerializeField]
    private Asteroid asteroidPrefab;

    [Min(float.Epsilon)]
    [SerializeField]
    private float levelSize = 5;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float asteroidPadding = 15;
    
    [Min(float.Epsilon)]
    [SerializeField]
    private float spawnRate = 1;
    
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

    private Material _lineMaterial;

    protected override void Awake()
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
        
        base.Awake();
    }

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

        _move = false;
        _turn = Turn.None;
        _shoot = false;
        _canShoot = true;
        StopAllCoroutines();
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

            // Calculate a random variance in the asteroid's rotation which will
            // cause its trajectory to change
            Quaternion rotation = Quaternion.AngleAxis(Random.Range(-trajectoryVariance, trajectoryVariance), Vector3.forward);

            // Create the new asteroid by cloning the prefab and set a random
            // size within the range
            Asteroid asteroid = Instantiate(asteroidPrefab, spawnDirection * (levelSize +asteroidPadding), rotation);
            asteroid.size = Random.Range(asteroid.minSize, asteroid.maxSize);

            // Set the trajectory to move in the direction of the spawner
            asteroid.SetTrajectory(rotation * -spawnDirection);

            _elapsedTime = 0;
        }
        
        AddReward(Mathf.Pow(Mathf.Max(0, levelSize - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y))) / levelSize, 2) * deltaTime);
        
        RequestDecision();
        
        if (_move)
        {
            body.AddForce(t.up * moveSpeed);
            AddReward(-moveCost * deltaTime);
        }
        
        if (_turn != Turn.None)
        {
            body.AddTorque(turnSpeed * (_turn == Turn.Left ? 1 : -1));
            AddReward(-turnCost * deltaTime);
        }

        if (!_canShoot || !_shoot)
        {
            return;
        }
        
        AddReward(-shootCost);
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EndEpisode();
    }

    public void DestroyedAsteroid()
    {
        AddReward(asteroidScore);
    }

    private void OnRenderObject()
    {
        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        
        GL.Color(Color.green);

        Vector3 offset = transform.localScale / 2;
        
        GL.Vertex(new(-levelSize - offset.x, levelSize + offset.y, 0));
        GL.Vertex(new(levelSize + offset.x, levelSize + offset.y, 0));
        
        GL.Vertex(new(levelSize + offset.x, levelSize + offset.y, 0));
        GL.Vertex(new(levelSize + offset.x, -levelSize - offset.y, 0));
        
        GL.Vertex(new(levelSize + offset.x, -levelSize - offset.y, 0));
        GL.Vertex(new(-levelSize - offset.x, -levelSize - offset.y, 0));
        
        GL.Vertex(new(-levelSize - offset.x, -levelSize - offset.y, 0));
        GL.Vertex(new(-levelSize - offset.x, levelSize + offset.y, 0));
        
        GL.End();
        GL.PopMatrix();
    }

    private void OnGUI()
    {
        GUI.Label(new(Screen.width / 2 - 25, 10, 50, 20), $"{GetCumulativeReward()}");
    }
}