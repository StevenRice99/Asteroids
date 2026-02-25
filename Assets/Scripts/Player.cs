using System.Collections;
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
    private float shootDelay = 0.2f;
    
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
    /// If the agent can shoot.
    /// </summary>
    private bool _canShoot = true;
    
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
        _canShoot = true;
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

        bool wKey = Keyboard.current.wKey.isPressed;
        bool aKey = Keyboard.current.aKey.isPressed;
        bool sKey = Keyboard.current.sKey.isPressed;
        bool dKey = Keyboard.current.dKey.isPressed;
        bool space = Keyboard.current.spaceKey.isPressed;
        
        // "W" to move forward, otherwise don't move.
        discreteActions[0] = wKey ? 1 : 0;
        
        // See if we have chosen to perform a manual move. We also check the S key, despite not being able to move backwards, as a means to "hold" our movement.
        if (wKey || sKey || aKey || dKey || space)
        {
            // Apply the turn. "A" to move left, "D" to move right, and neither to not turn.
            discreteActions[1] = (int) (aKey ? dKey ? Turn.None : Turn.Left : dKey ? Turn.Right : Turn.None);
            
            // Space to shoot, otherwise do not shoot.
            discreteActions[2] = space ? 1 : 0;
            return;
        }
        
        Transform t = transform;
        Vector3 raw = t.position;
        Vector2 p = new(raw.x, raw.y);
        
        // Find the nearest asteroid to automatically aim at.
        GameObject nearest = Spawned.Where(s => s.CompareTag("Asteroid")).OrderBy(s =>
        {
            Vector3 position = s.transform.position;
            return Vector2.Distance(p, new(position.x, position.y));
        }).FirstOrDefault();
        
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
        discreteActions[1] = (int)(Vector3.Cross(aimDirection.normalized, t.up).z < 0 ? Turn.Left : Turn.Right);
        
        // Shoot if the player is closely aligned with the predicted intercept path which has been set to five degrees.
        discreteActions[2] = Vector3.Angle(t.up, aimDirection) < 5f ? 1 : 0;
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
        
        _elapsedTime += Time.fixedDeltaTime;
        
        // If enough time has passed, spawn a new asteroid.
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

        // If the player cannot shoot or the agent did not request to shoot, return.
        if (!_canShoot || !_shoot)
        {
            return;
        }
        
        // Give a slight penalty for firing to ensure the agent learns to not just spam fire.
        AddReward(-0.1f);
        
        // Create a new bullet.
        Bullet bullet = Instantiate(bulletPrefab, p, t.rotation);
        bullet.name = "Bullet";
        bullet.Initialize(t.up, this);
        
        // Start the cooldown to shoot again.
        StopAllCoroutines();
        StartCoroutine(ShootCooldown());
        return;
        
        // Delay to shoot again.
        IEnumerator ShootCooldown()
        {
            _canShoot = false;
            yield return new WaitForSeconds(shootDelay);
            _canShoot = true;
        }
    }
    
    /// <summary>
    /// Sent when an incoming collider makes contact with this object's collider (2D physics only). This function can be a coroutine.
    /// </summary>
    /// <param name="_">The Collision2D data associated with this collision.</param>
    private void OnCollisionEnter2D(Collision2D _)
    {
        // End the episode if an asteroid is hit and give a penalty.
        AddReward(-1f);
        EndEpisode();
    }
    
    /// <summary>
    /// If an asteroid was destroyed, increase the score.
    /// </summary>
    public void DestroyedAsteroid()
    {
        // Simply add one score for every asteroid.
        AddReward(1f);
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
        
        // Display the score in the top left.
        GUI.Label(new(x, x, w, h), $"Score = {GetCumulativeReward()}");
        
        if (models.Length < 1 || (Academy.IsInitialized && Academy.Instance.IsCommunicatorOn))
        {
            return;
        }
        
        float xr = Screen.width - x - w;
        float y = x;
        
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
            if (GUI.Button(new(xr, y, w, h), "Heuristic"))
            {
                _parameters.Model = null;
                _parameters.BehaviorType = BehaviorType.HeuristicOnly;
            }
        }
    }
}