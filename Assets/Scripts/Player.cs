using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

/// <summary>
/// The player which also controls the spawning of asteroids and bullets.
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
[RequireComponent(typeof(BehaviorParameters))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class Player : Agent
{
    /// <summary>
    /// Options for turning the player.
    /// </summary>
    private enum Turn
    {
        None,
        Left,
        Right
    }
    
    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private readonly int _srcBlend = Shader.PropertyToID("_SrcBlend");

    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private readonly int _dstBlend = Shader.PropertyToID("_DstBlend");

    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private readonly int _cull = Shader.PropertyToID("_Cull");

    /// <summary>
    /// Cached shader value for use with line rendering.
    /// </summary>
    private readonly int _zWrite = Shader.PropertyToID("_ZWrite");
    
    /// <summary>
    /// All trained models.
    /// </summary>
    [Tooltip("All trained models.")]
    [SerializeField]
    private ModelAsset[] models;
    
    /// <summary>
    /// Prefab for the asteroids.
    /// </summary>
    [Header("Requirements")]
    [Tooltip("Prefab for the asteroids.")]
    [SerializeField]
    private Asteroid asteroidPrefab;
    
    /// <summary>
    /// Prefab for the bullets.
    /// </summary>
    [Tooltip("Prefab for the bullets.")]
    [SerializeField]
    private Bullet bulletPrefab;
    
    /// <summary>
    /// The rigidbody for the player.
    /// </summary>
    [Tooltip("The rigidbody for the player.")]
    [SerializeField]
    private Rigidbody2D body;
    
    /// <summary>
    /// The size of the level.
    /// </summary>
    [Header("Level")]
    [Tooltip("The size of the level.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float size = 5;
    
    /// <summary>
    /// How much outside of the level to spawn the asteroids.
    /// </summary>
    [Tooltip("How much outside of the level to spawn the asteroids.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float padding = 15;
    
    /// <summary>
    /// The max angle asteroids can spawn offset of the level origin.
    /// </summary>
    [Tooltip("The max angle asteroids can spawn offset of the level origin.")]
    [Range(0f, 45f)]
    [SerializeField]
    private float angle = 15;
    
    /// <summary>
    /// How many seconds to wait before spawning an asteroid.
    /// </summary>
    [Tooltip("How many seconds to wait before spawning an asteroid.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float spawnRate = 1;
    
    /// <summary>
    /// How fast to move the player.
    /// </summary>
    [Header("Controls")]
    [Tooltip("How fast to move the player.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float moveSpeed = 1;
    
    /// <summary>
    /// How fast to turn the player.
    /// </summary>
    [Tooltip("How fast to turn the player.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float turnSpeed = 0.1f;
    
    /// <summary>
    /// Delay before being able to shoot another bullet.
    /// </summary>
    [Tooltip("Delay before being able to shoot another bullet.")]
    [Min(float.Epsilon)]
    [SerializeField]
    private float shootDelay = 0.5f;
    
    /// <summary>
    /// The current shooting cooldown.
    /// </summary>
    private float _cooldown;
    
    /// <summary>
    /// Automatically aim and shoot at the nearest asteroid in heuristic mode.
    /// </summary>
    [Tooltip("Layer mask for the auto aiming.")]
    [SerializeField]
    private LayerMask layerMask;
    
    /// <summary>
    /// All asteroids and bullets spawned.
    /// </summary>
    public readonly List<GameObject> Spawned = new();
    
    /// <summary>
    /// If the agent should move this frame.
    /// </summary>
    private bool _move;
    
    /// <summary>
    /// Which way the agent should turn this frame.
    /// </summary>
    private Turn _turn;
    
    /// <summary>
    /// If the agent should shoot this frame.
    /// </summary>
    private bool _shoot;
    
    /// <summary>
    /// How much time has past since the last asteroid was spawned.
    /// </summary>
    private float _elapsedTime;
    
    /// <summary>
    /// Material for rendering the outline of the level.
    /// </summary>
    private Material _lineMaterial;
    
    /// <summary>
    /// The parameters for this agent.
    /// </summary>
    private BehaviorParameters _parameters;
    
    /// <summary>
    /// Implement OnEpisodeBegin() to set up an Agent instance at the beginning of an episode.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Cleanup any remaining asteroids and bullets from past episodes.
        foreach (GameObject s in Spawned)
        {
            Destroy(s);
        }
        
        Spawned.Clear();
        
        // Move back to the middle of the level.
        Transform t = transform;
        t.position = Vector3.zero;
        t.rotation = Quaternion.identity;
        
        // Reset any velocity.
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        
        // Reset that any time has passed.
        _elapsedTime = 0;
        
        // Reset all inputs.
        _move = false;
        _turn = Turn.None;
        _shoot = false;
        _cooldown = 0;
        StopAllCoroutines();
    }
    
    /// <summary>
    /// Implement Heuristic(ActionBuffers) to choose an action for this agent using a custom heuristic.
    /// </summary>
    /// <param name="actionsOut">The ActionBuffers which contain the continuous and discrete action buffers to write to.</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Implement keyboard controls.
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        bool forward = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
        bool left = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
        bool right = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
        bool fire = Keyboard.current.spaceKey.isPressed;
        bool nothing = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
        
        // "W" or the up arrow to move forward, otherwise don't move.
        discreteActions[0] = forward ? 1 : 0;
        
        // See if we have chosen to perform a manual move. We also check the S key, despite not being able to move backwards, as a means to "hold" our movement.
        if (forward || left || right || fire || nothing)
        {
            // Apply the turn. "A" or left arrow to move left, "D" or right arrow to move right, and neither to not turn.
            discreteActions[1] = (int) (left ? right ? Turn.None : Turn.Left : right ? Turn.Right : Turn.None);
            
            // Space to shoot, otherwise do not shoot.
            discreteActions[2] = fire ? 1 : 0;
            return;
        }
        
        Transform t = transform;
        Vector3 raw = t.position;
        Vector2 p = new(raw.x, raw.y);
        
        // Calculate the circumscribed circle radius for the player's box collider.
        BoxCollider2D playerCol = GetComponent<BoxCollider2D>();
        float playerRadius = playerCol != null ? playerCol.bounds.extents.magnitude : t.localScale.x / 2f;
        
        // Find the nearest asteroid, prioritizing those on a collision course.
        var threatInfo = Spawned
            .Where(s => s.CompareTag("Asteroid"))
            .Select(s =>
            {
                Vector2 aPos = s.transform.position;
                Vector2 aVel = s.GetComponent<Rigidbody2D>().linearVelocity;
                Vector2 toPlayer = p - aPos;
                
                float distanceToPlayer = toPlayer.magnitude;
                
                // Determine if the asteroid is moving towards the player.
                float dot = Vector2.Dot(toPlayer, aVel);
                bool movingTowards = dot > 0;
                
                // Calculate the shortest distance from the player to the asteroid's trajectory line.
                float distanceToTrajectory = float.MaxValue;
                if (movingTowards && aVel.sqrMagnitude > 0.001f)
                {
                    distanceToTrajectory = Mathf.Abs(toPlayer.x * aVel.y - toPlayer.y * aVel.x) / aVel.magnitude;
                }
                
                // Calculate the circumscribed circle radius for the asteroid's box collider.
                BoxCollider2D asteroidCol = s.GetComponent<BoxCollider2D>();
                float asteroidRadius = asteroidCol != null ? asteroidCol.bounds.extents.magnitude : s.transform.localScale.x / 2f;
                
                // Flag as threat if it's moving towards us AND the trajectory intersects our combined bounding radii.
                bool isThreat = movingTowards && distanceToTrajectory < playerRadius + asteroidRadius;
                
                // Calculate time to collision for threats.
                float timeToCollision = float.MaxValue;
                if (isThreat && aVel.sqrMagnitude > 0.001f)
                {
                    timeToCollision = dot / aVel.sqrMagnitude;
                }
                
                return new { GameObject = s, IsThreat = isThreat, Distance = distanceToPlayer, TimeToCollision = timeToCollision };
            })
            // Order by threats first, then sort by time to collision, then distance.
            .OrderByDescending(x => x.IsThreat) 
            .ThenBy(x => x.TimeToCollision)
            .ThenBy(x => x.Distance)
            .FirstOrDefault();
        
        GameObject nearest = threatInfo?.GameObject;
        
        // Nothing to do if no aiming target.
        if (nearest == null)
        {
            discreteActions[1] = (int)Turn.None;
            discreteActions[2] = 0;
            return;
        }
        
        // Calculate an intercept.
        Vector2 targetPos = nearest.transform.position;
        Vector2 targetVel = nearest.GetComponent<Rigidbody2D>().linearVelocity;
        Vector2 deltaP = targetPos - p;
        
        // Quadratic equation coefficients.
        float a = targetVel.sqrMagnitude - bulletPrefab.Speed * bulletPrefab.Speed;
        float b = 2f * Vector2.Dot(deltaP, targetVel);
        float c = deltaP.sqrMagnitude;
        
        // Fallback to direct line of sight.
        Vector3 aimDirection = deltaP;
        
        float determinant = b * b - 4f * a * c;
        if (determinant > 0f)
        {
            // Calculate both possible times.
            float t1 = (-b + Mathf.Sqrt(determinant)) / (2f * a);
            float t2 = (-b - Mathf.Sqrt(determinant)) / (2f * a);
            
            // We want the smallest positive time.
            float timeToIntercept = -1f;
            switch (t1)
            {
                case > 0f when t2 > 0f:
                    timeToIntercept = Mathf.Min(t1, t2);
                    break;
                case > 0f:
                    timeToIntercept = t1;
                    break;
                default:
                {
                    if (t2 > 0f) timeToIntercept = t2;
                    break;
                }
            }
            
            if (timeToIntercept > 0f)
            {
                // Calculate the future position.
                aimDirection = (Vector3)(targetPos + targetVel * timeToIntercept) - raw;
            }
        }
        
        // Turn towards the calculated intercept direction.
        float angleToTarget = Vector3.Angle(t.up, aimDirection);
        float side = Vector3.Cross(aimDirection.normalized, t.up).z;
        Turn desiredTurn = side < 0 ? Turn.Left : Turn.Right;
        
        switch (angleToTarget)
        {
            // If we're close to the target, use angular velocity to damp the rotation and prevent overshooting.
            case < 7.5f when Mathf.Abs(body.angularVelocity) > 10f:
            {
                bool rotatingTowardsTarget = (side < 0 && body.angularVelocity > 0) || (side > 0 && body.angularVelocity < 0);
                discreteActions[1] = (int)(rotatingTowardsTarget ? (desiredTurn == Turn.Left ? Turn.Right : Turn.Left) : Turn.None);
                break;
            }
            case < 1f:
                discreteActions[1] = (int)Turn.None;
                break;
            default:
                discreteActions[1] = (int)desiredTurn;
                break;
        }
        
        // If it's a threat, and we're not yet facing it, check if we'll collide before we can face it.
        if (threatInfo is { IsThreat: true } && threatInfo.TimeToCollision < 0.75f)
        {
            angleToTarget = Vector3.Angle(t.up, aimDirection);
            if (angleToTarget > 30f)
            {
                discreteActions[0] = 1;
            }
        }
        
        // Shoot if any asteroid is closely aligned with its predicted intercept path.
        float bulletSpeed = bulletPrefab.Speed;
        discreteActions[2] = Spawned
            .Where(s => s.CompareTag("Asteroid"))
            .Any(asteroid =>
            {
                targetPos = asteroid.transform.position;
                targetVel = asteroid.GetComponent<Rigidbody2D>().linearVelocity;
                deltaP = targetPos - p;
                
                float aS = targetVel.sqrMagnitude - bulletSpeed * bulletSpeed;
                float bS = 2f * Vector2.Dot(deltaP, targetVel);
                float cS = deltaP.sqrMagnitude;
                
                float det = bS * bS - 4f * aS * cS;
                if (det <= 0f)
                {
                    return false;
                }
                
                float sqrtDet = Mathf.Sqrt(det);
                float t1 = (-bS + sqrtDet) / (2f * aS);
                float t2 = (-bS - sqrtDet) / (2f * aS);
                
                float tIntercept = -1f;
                switch (t1)
                {
                    case > 0f when t2 > 0f:
                        tIntercept = Mathf.Min(t1, t2);
                        break;
                    case > 0f:
                        tIntercept = t1;
                        break;
                    default:
                    {
                        if (t2 > 0f) tIntercept = t2;
                        break;
                    }
                }
                
                if (tIntercept <= 0f)
                {
                    return false;
                }
                
                Vector3 interceptDir = (Vector3)(targetPos + targetVel * tIntercept) - raw;
                return Vector3.Angle(t.up, interceptDir) < 5f;
            }) ? 1 : 0;
    }
    
    /// <summary>
    /// Implement CollectObservations() to collect the vector observations of the agent for the step.
    /// The agent observation describes the current environment from the perspective of the agent.
    /// </summary>
    /// <param name="sensor">The vector observations for the agent.</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // Add the position relative to the level boundaries.
        // Position is scaled between zero and one for both X and Y values.
        Vector3 p = transform.localPosition;
        sensor.AddObservation((p.x / size + 1f) / 2f);
        sensor.AddObservation((p.y / size + 1f) / 2f);
        
        // Add the rotation scaled between zero and one.
        sensor.AddObservation(transform.localEulerAngles.z / 360f);
    }
    
    /// <summary>
    /// Implement OnActionReceived() to specify agent behavior at every step, based on the provided action.
    /// </summary>
    /// <param name="actions">Struct containing the buffers of actions to be executed at this step.</param>
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Set if it should move.
        _move = actions.DiscreteActions[0] > 0;
        
        // Cast the result to a turn value.
        _turn = (Turn) actions.DiscreteActions[1];
        
        // Set if it should try to shoot.
        _shoot = actions.DiscreteActions[2] > 0;
    }
    
    /// <summary>
    /// Write a mask to block shooting when the agent is not able to.
    /// </summary>
    /// <param name="actionMask">The actions mask.</param>
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // Can always move forwards and back.
        actionMask.SetActionEnabled(0, 0, true);
        actionMask.SetActionEnabled(0, 1, true);
        
        // Can always turn.
        actionMask.SetActionEnabled(1, 0, true);
        actionMask.SetActionEnabled(1, 1, true);
        actionMask.SetActionEnabled(1, 2, true);
        
        // Can only shoot if not on cooldown.
        actionMask.SetActionEnabled(2, 0, true);
        actionMask.SetActionEnabled(2, 1, _cooldown <= 0);
    }
    
    /// <summary>
    /// Awake is called when an enabled script instance is being loaded.
    /// </summary>
    protected override void Awake()
    {
        // Unity has a built-in shader that is useful for drawing simple colored things.
        _lineMaterial = new(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        
        // Turn on alpha blending.
        _lineMaterial.SetInt(_srcBlend, (int)BlendMode.SrcAlpha);
        _lineMaterial.SetInt(_dstBlend, (int)BlendMode.OneMinusSrcAlpha);
        
        // Turn backface culling off.
        _lineMaterial.SetInt(_cull, (int)CullMode.Off);
        
        // Turn off depth writes.
        _lineMaterial.SetInt(_zWrite, 0);
        
        // Get the parameters.
        _parameters = GetComponent<BehaviorParameters>();
        
        // If there is a demonstration recorder, set it outside of the assets folder.
        DemonstrationRecorder recorder = GetComponent<DemonstrationRecorder>();
        if (recorder)
        {
            string path = Path.GetDirectoryName(Application.dataPath);
            if (path != null)
            {
                path = Path.Combine(path, "Demonstrations");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                
                recorder.DemonstrationDirectory = path;
            }
        }
        
        base.Awake();
    }
    
    /// <summary>
    /// Frame-rate independent MonoBehaviour.FixedUpdate message for physics calculations.
    /// </summary>
    private void FixedUpdate()
    {
        // Clean up asteroids and bullets that have gone too far.
        for (int i = 0; i < Spawned.Count; i++)
        {
            Vector3 s = Spawned[i].transform.position;
            if (Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)) <= size + padding)
            {
                continue;
            }
            
            Destroy(Spawned[i].gameObject);
            Spawned.RemoveAt(i--);
        }
        
        Transform t = transform;
        Vector3 p = t.position;
        
        // Keep the player in bounds.
        t.position = new(Mathf.Clamp(p.x, -size, size), Mathf.Clamp(p.y, -size, size), 0);
        
        float delta = Time.fixedDeltaTime;
        // If enough time has passed, spawn a new asteroid.
        _elapsedTime += delta;
        if (_elapsedTime >= spawnRate)
        {
            // Choose a random direction from the center of the level.
            Vector2 spawnDirection = Random.insideUnitCircle.normalized;
            
            // Give the direction a random offset so it is not guaranteed to be aimed at the exact middle.
            Quaternion rotation = Quaternion.AngleAxis(Random.Range(-angle, angle), Vector3.forward);
            
            // Create a new asteroid at the spawn distance with the given rotation.
            Asteroid asteroid = Instantiate(asteroidPrefab, spawnDirection * (size + padding), rotation);
            asteroid.name = "Asteroid";
            
            // Give the asteroid a random size.
            asteroid.Size = Random.Range(asteroid.sizes.x, asteroid.sizes.y);
            
            // Move the asteroid towards the level.
            asteroid.Initialize(rotation * -spawnDirection, this);
            
            // Reset the timer.
            _elapsedTime = 0;
        }
        
        // Get a decision from the player, being either the heuristic, results from PyTorch, or finished model inference.
        RequestDecision();
        
        // Move if set to.
        if (_move)
        {
            body.AddForce(t.up * moveSpeed);
        }
        
        // If it should be turning, do so.
        if (_turn != Turn.None)
        {
            body.AddTorque(turnSpeed * (_turn == Turn.Left ? 1 : -1));
        }
        
        // Handle recharging the attack.
        if (_cooldown > 0)
        {
            _cooldown -= delta;
        }
        
        // If the player cannot shoot or the agent did not request to shoot, return.
        if (_cooldown > 0 || !_shoot)
        {
            // When we don't shoot, give a slight reward for surviving and not shooting.
            return;
        }
        
        // Put the attack on cooldown.
        _cooldown = shootDelay;
        
        // Give a slight penalty for firing to ensure the agent learns to not just spam fire.
        AddReward(-0.1f);
        
        // Create a new bullet.
        Bullet bullet = Instantiate(bulletPrefab, p, t.rotation);
        bullet.name = "Bullet";
        bullet.Initialize(t.up, this);
    }
    
    /// <summary>
    /// Sent when an incoming collider makes contact with this object's collider (2D physics only). This function can be a coroutine.
    /// </summary>
    /// <param name="_">The Collision2D data associated with this collision.</param>
    private void OnCollisionEnter2D(Collision2D _)
    {
        // End the episode if an asteroid is hit and give a penalty.
        SetReward(-1f);
        EndEpisode();
    }
    
    /// <summary>
    /// If an asteroid was destroyed, increase the score.
    /// </summary>
    public void DestroyedAsteroid()
    {
        // Add a score for every asteroid.
        AddReward(0.5f);
    }
    
    /// <summary>
    /// OnRenderObject is called after camera has rendered the Scene.
    /// </summary>
    private void OnRenderObject()
    {
        // Setup to render borders.
        _lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        
        // Make borders green.
        GL.Color(Color.green);
        
        // Add padding offset for the width of the player.
        Vector3 offset = transform.localScale / 2;
        
        // Top.
        GL.Vertex(new(-size - offset.x, size + offset.y, 0));
        GL.Vertex(new(size + offset.x, size + offset.y, 0));
        
        // Right.
        GL.Vertex(new(size + offset.x, size + offset.y, 0));
        GL.Vertex(new(size + offset.x, -size - offset.y, 0));
        
        // Bottom.
        GL.Vertex(new(size + offset.x, -size - offset.y, 0));
        GL.Vertex(new(-size - offset.x, -size - offset.y, 0));
        
        // Left.
        GL.Vertex(new(-size - offset.x, -size - offset.y, 0));
        GL.Vertex(new(-size - offset.x, size + offset.y, 0));
        
        // Finish rendering.
        GL.End();
        GL.PopMatrix();
    }
    
    /// <summary>
    /// OnGUI is called for rendering and handling GUI events.
    /// </summary>
    private void OnGUI()
    {
        const float x = 10;
        const float w = 175;
        const float h = 25;
        float y = x;
        
        // Display the score in the top left.
        GUI.Label(new(x, y, w, h), $"Score = {GetCumulativeReward()}");
        
        // Display the current time step.
        y += h + x;
        GUI.Label(new(x, y, w, h), $"Steps = {StepCount}");
        
        if (models.Length < 1 || (Academy.IsInitialized && Academy.Instance.IsCommunicatorOn))
        {
            return;
        }
        
        // Allow to reset.
        y += h + x;
        if (GUI.Button(new(x, y, w, h), "Reset"))
        {
            EndEpisode();
        }
        
        float xr = Screen.width - x - w;
        
        // Handle based on if we are currently using a model or not.
        bool heuristic = _parameters.IsInHeuristicMode();
        if (heuristic)
        {
            // Indicate we are in heuristic mode.
            GUI.Label(new(xr, y, w, h), "Heuristic");
            y += h + x;
            
            // Give options to switch to all the other models.
            foreach (ModelAsset model in models)
            {
                if (GUI.Button(new(xr, y, w, h), model.name))
                {
                    _parameters.Model = model;
                    _parameters.BehaviorType = BehaviorType.InferenceOnly;
                }
                
                y += h + x;
            }
        }
        else
        {
            // Display the name of the current model.
            ModelAsset model = _parameters.Model;
            if (model != null)
            {
                GUI.Label(new(xr, y, w, h), model.name);
                y += h + x;
            }
            
            // Display all other models which can be switched to.
            foreach (ModelAsset other in models)
            {
                if (model == other)
                {
                    continue;
                }
                
                if (GUI.Button(new(xr, y, w, h), other.name))
                {
                    _parameters.Model = other;
                    _parameters.BehaviorType = BehaviorType.InferenceOnly;
                }
                
                y += h + x;
            }
            
            // Display the option to switch to heuristic mode.
            if (!GUI.Button(new(xr, y, w, h), "Heuristic"))
            {
                return;
            }
            
            _parameters.Model = null;
            _parameters.BehaviorType = BehaviorType.HeuristicOnly;
        }
    }
}