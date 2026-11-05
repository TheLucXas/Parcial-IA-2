using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Agent : MonoBehaviour
{
    #region Enums
    private enum SteeringModes { Seek, Flee, Arrive, Pursuit, Evade, Flocking }
    #endregion

    #region Serialized Fields
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 2.0f;
    [SerializeField] private float _attackDamage = 0.25f;
    [SerializeField] private float _attackCooldown = 0.5f;
    [SerializeField] private float _respawnTime = 6.0f;
    [SerializeField]
    private float _maxSpeed = 5f;
    [SerializeField]
    private float _maxForce = 10f;
    [SerializeField]
    private float _viewRadius = 5f;
    [SerializeField]
    private SteeringModes _currentMode;
    [SerializeField]
    private float _arriveRadius = 3f;
    [SerializeField] private Color _gizmosFlockingColor = Color.purple;
    [SerializeField] private Color _gizmosNeighborsColor = Color.cyan;

    [Header("Perception")]
    [SerializeField] private TriggerSensor _sensor;
    [SerializeField] private LayerMask _wallMask;

    [Header("Evade Stats")]
    [SerializeField] private float _evadeSpeed = 8f;
    [SerializeField] private float _evadeForce = 15f;

    [Header("Flocking values")]
    [SerializeField]
    private float _flockingRadius = 6f;
    [SerializeField]
    private float _separationRadius = 2f;
    [SerializeField, Range(0f, 3f)]
    private float _separationWeight = 1f;
    [SerializeField, Range(0f, 3f)]
    private float _cohesionWeight = 1f;
    [SerializeField, Range(0f, 3f)]
    private float _alignmentWeight = 1f;

    [Header("Visual Feedback")]
    [SerializeField] private Sprite[] _agentSprites;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private TextMeshProUGUI _textMeshPro;
    #endregion

    #region Private Fields
    private float _currentHealth;
    private bool _isDead = false;

    private readonly List<Agent> _neighbors = new();
    private readonly List<Ball> _nearbyBalls = new();
    private HunterAgent _nearbyHunter;
    private bool _isCollected;

    private Color _originalColor;
    private Coroutine _damageFlashCoroutine;

    private float _timer;
    private Vector3 _velocity;
    private int _originalLayer;
    #endregion

    #region Properties
    public Vector3 Velocity => _velocity;
    public bool IsDead => _isDead;
    public bool IsAvailable => !_isCollected && gameObject.activeInHierarchy;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _currentHealth = _maxHealth;
        _originalLayer = gameObject.layer;
        if (_spriteRenderer != null) _originalColor = _spriteRenderer.color;
    }

    private void OnEnable()
    {
        if (_sensor == null) return;
        _sensor.SetRadius(_viewRadius);
        _sensor.OnEnter += HandleSensorEnter;
        _sensor.OnExit += HandleSensorExit;
    }

    private void OnDisable()
    {
        if (_sensor == null) return;
        _sensor.OnEnter -= HandleSensorEnter;
        _sensor.OnExit -= HandleSensorExit;
    }

    private void Start()
    {
        Vector3 randomVector = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        _velocity += randomVector.normalized * _maxSpeed;
        _timer = _attackCooldown;
        int spriteIndex = Random.Range(0, _agentSprites.Length);
        if (_agentSprites != null && _spriteRenderer != null) _spriteRenderer.sprite = _agentSprites[spriteIndex];
    }

    private void Update()
    {
        if (_isDead) return;

        Vector3 steeringForce = Vector3.zero;

        Ball ball = GetClosestBall();

        if (CanSeeHunter(out HunterAgent hunter))
        {
            UpdateStateText(SteeringModes.Evade, "Evade");

            steeringForce += SteeringUtils.CalculateEvade(
                transform.position,
                _velocity,
                _evadeSpeed,
                _evadeForce,
                hunter.transform.position,
                hunter.CurrentVelocity
            );
        }
        else if (ball != null)
        {
            UpdateStateText(SteeringModes.Arrive, "Gather/Arrive");

            Vector3 ballPosition = ball.transform.position;
            Vector3 flatBallPos = new Vector3(ballPosition.x, 0f, ballPosition.z);

            steeringForce += CalculateArrive(flatBallPos);
            steeringForce += CalculateSeparation(_neighbors, _separationRadius) * _separationWeight;

            if (Vector3.Distance(transform.position, ballPosition) <= _arriveRadius)
            {
                _velocity = Vector3.zero;
                _timer += Time.deltaTime;

                if (_timer >= _attackCooldown)
                {
                    _timer = 0f;
                    ball.TakeDamage(_attackDamage);
                }
                return;
            }
        }
        else
        {
            UpdateStateText(SteeringModes.Flocking, "Flocking");

            steeringForce += CalculateFlocking();
        }

        _velocity += steeringForce;
        _velocity.y = 0f;

        transform.position += _velocity * Time.deltaTime;

        if (_velocity.sqrMagnitude > 0.0001f)
        {
            transform.forward = _velocity.normalized;
        }

        transform.position = Bounds.Instance.CalculateBoundPosition(transform.position);
    }

    private void OnDrawGizmos()
    {
        if (_isDead) return;

        Vector3 pos2D = new Vector3(transform.position.x, 0f, transform.position.z);

        Gizmos.color = Color.white;
        GizmosUtils.DrawGizmosCircle(pos2D, Vector3.up, _viewRadius);

        Gizmos.color = _gizmosNeighborsColor;
        GizmosUtils.DrawGizmosCircle(pos2D, Vector3.up, _flockingRadius);

        Gizmos.color = _gizmosFlockingColor;
        GizmosUtils.DrawGizmosCircle(pos2D, Vector3.up, _separationRadius);
    }
    #endregion

    #region Public Methods
    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _currentHealth -= amount;

        if (_spriteRenderer != null)
        {
            if (_damageFlashCoroutine != null) StopCoroutine(_damageFlashCoroutine);
            _damageFlashCoroutine = StartCoroutine(DamageFlash());
        }

        if (_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            _isDead = true;
            _velocity = Vector3.zero;
            gameObject.layer = LayerMask.NameToLayer("Gather");

            if (_spriteRenderer != null) _spriteRenderer.color = Color.red;

            if (_textMeshPro != null) _textMeshPro.text = "Dead";
        }
    }

    public void Collect()
    {
        _isCollected = true;
        AgentVisibility(false);
        StartCoroutine(WaitToRespawn());
    }

    public void Respawn()
    {
        transform.position = Bounds.Instance.GetRandomPointInBounds();
        _currentHealth = _maxHealth;
        _isDead = false;
        _isCollected = false;
        gameObject.layer = _originalLayer;

        if (_textMeshPro != null) _textMeshPro.text = "Flocking";

        if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

        AgentVisibility(true);
    }

    public void AgentVisibility(bool visible)
    {
        gameObject.layer = visible ? _originalLayer : 0;

        if (_spriteRenderer != null) _spriteRenderer.enabled = visible;

        if (_textMeshPro != null) _textMeshPro.text = "";
    }
    #endregion

    #region Private Methods
    private void HandleSensorEnter(Collider other)
    {
        if (other.TryGetComponent(out Agent agent))
        {
            if (agent != this && !_neighbors.Contains(agent)) _neighbors.Add(agent);
        }
        else if (other.TryGetComponent(out Ball ball))
        {
            if (!_nearbyBalls.Contains(ball)) _nearbyBalls.Add(ball);
        }
        else if (other.TryGetComponent(out HunterAgent hunter))
        {
            _nearbyHunter = hunter;
        }
    }

    private void HandleSensorExit(Collider other)
    {
        if (other.TryGetComponent(out Agent agent)) _neighbors.Remove(agent);
        else if (other.TryGetComponent(out Ball ball)) _nearbyBalls.Remove(ball);
        else if (other.TryGetComponent(out HunterAgent hunter) && hunter == _nearbyHunter) _nearbyHunter = null;
    }

    private bool CanSeeHunter(out HunterAgent hunter)
    {
        hunter = _nearbyHunter;
        if (hunter == null || !hunter.IsVisible) return false;
        if (!InRange(hunter.transform.position, _viewRadius)) return false;
        return PerceptionUtils.InLineOfSight(transform.position, hunter.transform.position, _wallMask);
    }

    private Ball GetClosestBall()
    {
        Ball closest = null;
        float closestSqr = float.MaxValue;

        for (int i = _nearbyBalls.Count - 1; i >= 0; i--)
        {
            Ball ball = _nearbyBalls[i];
            if (ball == null)
            {
                _nearbyBalls.RemoveAt(i);
                continue;
            }

            float sqr = (ball.transform.position - transform.position).sqrMagnitude;
            if (sqr <= _viewRadius * _viewRadius && sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = ball;
            }
        }

        return closest;
    }
    #endregion

    #region Coroutines
    private IEnumerator WaitToRespawn()
    {
        yield return new WaitForSeconds(_respawnTime);
        Respawn();
    }

    private IEnumerator DamageFlash()
    {
        if (_spriteRenderer == null) yield break;

        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.125f);
        _spriteRenderer.color = _originalColor;
        yield return new WaitForSeconds(0.125f);

        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.125f);
        _spriteRenderer.color = _originalColor;
        yield return new WaitForSeconds(0.125f);

        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.125f);

        if (!_isDead) _spriteRenderer.color = _originalColor;
    }
    #endregion

    #region Steering Behaviors

    private void UpdateStateText(SteeringModes newMode, string text)
    {
        if (_currentMode != newMode)
        {
            _currentMode = newMode;

            if (_textMeshPro != null)
            {
                _textMeshPro.text = text;
            }
        }
    }

    #region Flocking
    private Vector3 CalculateFlocking()
    {
        return CalculateSeparation(_neighbors, _separationRadius) * _separationWeight
             + CalculateAlignment(_neighbors, _flockingRadius) * _alignmentWeight
             + CalculateCohesion(_neighbors, _flockingRadius) * _cohesionWeight;
    }

    private Vector3 CalculateCohesion(IEnumerable<Agent> agents, float radius)
    {
        Vector3 desiredPosition = default;
        int count = 0;

        foreach (Agent item in agents)
        {
            if (item == this || item.IsDead || !item.IsAvailable) continue;
            if (InRange(item.transform.position, radius))
            {
                desiredPosition += item.transform.position;
                count++;
            }
        }

        if (count == 0) return Vector3.zero;

        desiredPosition /= count;

        return CalculateSeek(desiredPosition);
    }

    private Vector3 CalculateSeparation(IEnumerable<Agent> agents, float radius)
    {
        Vector3 desired = default;
        int count = 0;

        foreach (Agent item in agents)
        {
            if (item == this || item.IsDead || !item.IsAvailable) continue;
            if (InRange(item.transform.position, radius))
            {
                desired += (item.transform.position - transform.position);
                count++;
            }
        }

        if (count == 0) return Vector3.zero;

        desired /= count;

        return SteeringUtils.CalculateSteering(_velocity, -desired.normalized * _maxSpeed, _maxForce);
    }

    private Vector3 CalculateAlignment(List<Agent> agents, float radius)
    {
        Vector3 desired = default;
        int count = 0;

        foreach (Agent item in agents)
        {
            if (item == this || item.IsDead || !item.IsAvailable) continue;
            if (InRange(item.transform.position, radius))
            {
                desired += item.Velocity;
                count++;
            }
        }
        if (count == 0) return Vector3.zero;
        desired /= count;

        return SteeringUtils.CalculateSteering(_velocity, desired.normalized * _maxSpeed, _maxForce);
    }

    private bool InRange(Vector3 position, float radius) => (position - transform.position).sqrMagnitude <= radius * radius;

    #endregion

    private Vector3 CalculateSeek(Vector3 targetPosition) =>
        SteeringUtils.CalculateSeek(transform.position, _velocity, targetPosition, _maxSpeed, _maxForce);

    private Vector3 CalculateArrive(Vector3 targetPosition) =>
        SteeringUtils.CalculateArrive(transform.position, _velocity, targetPosition, _maxSpeed, _maxForce, _arriveRadius);

    #endregion
}