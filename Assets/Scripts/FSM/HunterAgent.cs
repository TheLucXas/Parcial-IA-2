using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum HunterStates { Search, Attack, Gather, Flee, Exit, Dead }

public class HunterAgent : MonoBehaviour
{
    #region Events
    public static event Action<HunterAgent> OnCaught;
    #endregion

    #region Serialized Fields
    [Header("Objective")]
    [SerializeField, Min(1)] private int _requiredGathers = 3;
    [SerializeField, Min(0f)] private float _respawnTime = 5f;
    [SerializeField, Min(0f)] private float _safeZoneWaitTime = 4f;

    [Header("Perception")]
    [SerializeField] private float _visionRadius = 10f;
    [SerializeField] private float _wolfDetectionRadius = 13f;
    [SerializeField] private float _perceptionInterval = 0.1f;
    [SerializeField] private TriggerSensor _sensor;
    [SerializeField] private LayerMask _wallMask;

    [Header("Navigation")]
    [SerializeField] private float _bodyRadius = 0.6f;
    [SerializeField] private float _nodeReachDistance = 1.5f;

    [Header("States")]
    [SerializeField] private SearchData _searchData;
    [SerializeField] private AttackData _attackData;
    [SerializeField] private GatherData _gatherData;
    [SerializeField] private FleeData _fleeData;
    [SerializeField] private ExitData _exitData;

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _meleeGizmoColor = Color.red;
    [SerializeField] private Color _rangeGizmoColor = Color.yellow;
    [SerializeField] private Color _radiusGizmoColor = Color.green;
    [SerializeField] private Color _wolfRadiusGizmoColor = new(1f, 0.5f, 0f);
    [SerializeField] private Color _pathGizmoColor = Color.cyan;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite[] _refereeSprites;
    [SerializeField] private TextMeshProUGUI _stateText;
    [SerializeField] private LineRenderer _pathLine;
    [SerializeField] private LineRenderer _alertLine;
    #endregion

    #region Private Fields
    private readonly FiniteStateMachine<HunterStates> _fsm = new();

    private readonly List<Agent> _nearbyBoids = new();
    private readonly List<WolfAgent> _nearbyWolves = new();
    private readonly List<Agent> _visibleBoids = new();
    private readonly List<Agent> _visibleDeadBoids = new();
    private readonly List<WolfAgent> _visibleWolves = new();

    private Collider _bodyCollider;
    private PathFollower _pathFollower;
    private Vector3 _startPosition;
    private float _perceptionTimer;
    private string _stateLabel = "";
    #endregion

    #region Properties
    public int RequiredGathers => _requiredGathers;
    public float RespawnTime => _respawnTime;
    public float SafeZoneWaitTime => _safeZoneWaitTime;
    public LayerMask WallMask => _wallMask;

    public HunterStates CurrentState => _fsm.CurrentStateID;
    public int Gathers { get; private set; }
    public int Deaths { get; private set; }
    public int CompletedExits { get; private set; }
    public float AttackCooldownTimer { get; set; } = 0f;
    public Vector3 CurrentVelocity { get; set; } = Vector3.zero;
    public bool IsCaught { get; private set; }
    public bool IsVisible { get; private set; } = true;
    public MapZone CurrentSafeZone { get; private set; }
    public bool IsInSafeZone => CurrentSafeZone != null;
    public bool IsInsideCoto => MapZone.IsInside(ZoneType.Coto, transform.position);
    public MapZone ChosenSafeZone { get; set; }
    public Agent CurrentTarget { get; set; }

    public PathFollower PathFollower => _pathFollower;
    public IReadOnlyList<Agent> VisibleBoids => _visibleBoids;
    public IReadOnlyList<Agent> VisibleDeadBoids => _visibleDeadBoids;
    public IReadOnlyList<WolfAgent> VisibleWolves => _visibleWolves;
    public bool SeesWolf => _visibleWolves.Count > 0;
    public string StateLabel => _stateLabel;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _bodyCollider = GetComponent<Collider>();
        _pathFollower = new PathFollower(_bodyRadius, _wallMask);
        _startPosition = transform.position;
    }

    private void OnEnable()
    {
        if (_sensor == null) return;
        _sensor.SetRadius(Mathf.Max(_visionRadius, _wolfDetectionRadius));
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
        _fsm.AddState(HunterStates.Search, new SearchState(_searchData, this));
        _fsm.AddState(HunterStates.Attack, new AttackState(_attackData, this));
        _fsm.AddState(HunterStates.Gather, new GatherState(_gatherData, this));
        _fsm.AddState(HunterStates.Flee, new FleeState(_fleeData, this));
        _fsm.AddState(HunterStates.Exit, new ExitState(_exitData, this));
        _fsm.AddState(HunterStates.Dead, new DeadState(this));
        _fsm.ChangeState(HunterStates.Search);

        if (_refereeSprites != null && _refereeSprites.Length > 0 && _spriteRenderer != null)
            _spriteRenderer.sprite = _refereeSprites[UnityEngine.Random.Range(0, _refereeSprites.Length)];
    }

    private void Update()
    {
        if (AttackCooldownTimer > 0f)
        {
            AttackCooldownTimer -= Time.deltaTime;
        }

        _perceptionTimer -= Time.deltaTime;
        if (_perceptionTimer <= 0f)
        {
            _perceptionTimer = _perceptionInterval;
            UpdatePerception();
        }

        _fsm.Update();

        _pathFollower.UpdateLine(_pathLine, transform.position);
        UpdateAlertLine();
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        var pos = new Vector3(transform.position.x, 0f, transform.position.z);
        Gizmos.color = _meleeGizmoColor;
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, _attackData.MeleeAttackRadius);

        Gizmos.color = _rangeGizmoColor;
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, _attackData.RangeAttackRadius);

        Gizmos.color = _radiusGizmoColor;
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, _visionRadius);

        Gizmos.color = _wolfRadiusGizmoColor;
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, _wolfDetectionRadius);

        if (!Application.isPlaying) return;

        _pathFollower.DrawGizmos(pos, _pathGizmoColor);

        Gizmos.color = Color.red;
        foreach (WolfAgent wolf in _visibleWolves)
        {
            if (wolf != null) Gizmos.DrawLine(pos, wolf.transform.position);
        }

        if (ChosenSafeZone != null)
        {
            Gizmos.color = MapZone.GetZoneColor(ZoneType.SafeZone);
            Gizmos.DrawWireSphere(ChosenSafeZone.Center, 3f);
        }

        if (CurrentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pos, CurrentTarget.transform.position);
        }
    }
    #endregion

    #region Public Methods
    public void UpdateStateText(string newStateName)
    {
        _stateLabel = newStateName;
        if (_stateText != null)
        {
            _stateText.text = $"{newStateName}\n{Gathers}/{_requiredGathers}";
        }
    }

    public void Catch()
    {
        if (!IsVisible || IsCaught) return;
        IsCaught = true;
    }

    public void AddGather() => Gathers++;

    public void ArriveTo(Vector3 target, float speed, float steerForce, float arriveRadius)
    {
        CurrentVelocity += SteeringUtils.CalculateArrive(transform.position, CurrentVelocity, PerceptionUtils.Flat(target), speed, steerForce, arriveRadius);
        ApplyMovement();
    }

    public void PursuitTarget(Agent target, float speed, float steerForce)
    {
        CurrentVelocity += SteeringUtils.CalculatePursuit(transform.position, CurrentVelocity, speed, steerForce, target.transform.position, target.Velocity);
        ApplyMovement();
    }

    public bool FollowPath(float speed, float steerForce)
    {
        if (!_pathFollower.HasDestination) return true;

        if (_pathFollower.HasArrived(transform.position, _nodeReachDistance))
        {
            CurrentVelocity = Vector3.zero;
            return true;
        }

        Vector3 target = _pathFollower.GetSteeringTarget(transform.position, _nodeReachDistance);
        CurrentVelocity += SteeringUtils.CalculateSeek(transform.position, CurrentVelocity, target, speed, steerForce);
        ApplyMovement();
        return false;
    }

    public bool SetDestination(Vector3 destination) => _pathFollower.SetDestination(transform.position, destination, null, false);

    public void StopMoving()
    {
        CurrentVelocity = Vector3.zero;
        _pathFollower.Clear();
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        if (_spriteRenderer != null) _spriteRenderer.enabled = visible;
        if (_bodyCollider != null) _bodyCollider.enabled = visible;
        if (!visible)
        {
            StopMoving();
            ClearPerception();
        }
    }

    public void NotifyCaught() => OnCaught?.Invoke(this);

    public void ResetAtStart()
    {
        transform.position = _startPosition;
        Physics.SyncTransforms();
        Gathers = 0;
        IsCaught = false;
        CurrentSafeZone = null;
        ChosenSafeZone = null;
        CurrentTarget = null;
        StopMoving();
        ClearPerception();
        SetVisible(true);
    }

    public void RegisterDeath() => Deaths++;

    public void RegisterExit() => CompletedExits++;

    public void SetSafeZone(MapZone zone) => CurrentSafeZone = zone;

    public void ClearSafeZone(MapZone zone)
    {
        if (CurrentSafeZone == zone) CurrentSafeZone = null;
    }

    public bool IsBoidVisible(Agent boid) => boid != null && _visibleBoids.Contains(boid);

    public bool IsDeadBoidVisible(Agent boid) => boid != null && _visibleDeadBoids.Contains(boid);

    public Agent GetClosest(IReadOnlyList<Agent> boids, bool dead)
    {
        Agent closest = null;
        float closestSqr = float.MaxValue;
        foreach (Agent boid in boids)
        {
            if (boid == null || !boid.IsAvailable || boid.IsDead != dead) continue;
            float sqr = (boid.transform.position - transform.position).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = boid;
            }
        }
        return closest;
    }
    #endregion

    #region Private Methods
    private void UpdatePerception()
    {
        _visibleBoids.Clear();
        _visibleDeadBoids.Clear();
        _visibleWolves.Clear();

        if (!IsVisible) return;

        Vector3 position = transform.position;

        if (IsInsideCoto)
        {
            for (int i = _nearbyBoids.Count - 1; i >= 0; i--)
            {
                Agent boid = _nearbyBoids[i];
                if (boid == null)
                {
                    _nearbyBoids.RemoveAt(i);
                    continue;
                }
                if (!boid.IsAvailable || !PerceptionUtils.InRange(position, boid.transform.position, _visionRadius)) continue;
                if (!PerceptionUtils.InLineOfSight(position, boid.transform.position, _wallMask)) continue;

                if (boid.IsDead) _visibleDeadBoids.Add(boid);
                else _visibleBoids.Add(boid);
            }
        }

        for (int i = _nearbyWolves.Count - 1; i >= 0; i--)
        {
            WolfAgent wolf = _nearbyWolves[i];
            if (wolf == null)
            {
                _nearbyWolves.RemoveAt(i);
                continue;
            }
            if (!PerceptionUtils.InRange(position, wolf.transform.position, _wolfDetectionRadius)) continue;
            if (!PerceptionUtils.InLineOfSight(position, wolf.transform.position, _wallMask)) continue;

            _visibleWolves.Add(wolf);
        }
    }

    private void ClearPerception()
    {
        _visibleBoids.Clear();
        _visibleDeadBoids.Clear();
        _visibleWolves.Clear();
    }

    private void HandleSensorEnter(Collider other)
    {
        if (other.TryGetComponent(out Agent boid))
        {
            if (!_nearbyBoids.Contains(boid)) _nearbyBoids.Add(boid);
        }
        else if (other.TryGetComponent(out WolfAgent wolf))
        {
            if (!_nearbyWolves.Contains(wolf)) _nearbyWolves.Add(wolf);
        }
    }

    private void HandleSensorExit(Collider other)
    {
        if (other.TryGetComponent(out Agent boid)) _nearbyBoids.Remove(boid);
        else if (other.TryGetComponent(out WolfAgent wolf)) _nearbyWolves.Remove(wolf);
    }

    private void ApplyMovement()
    {
        Vector3 velocity = CurrentVelocity;
        SteeringUtils.ApplyVelocity(transform, ref velocity, _bodyRadius * 0.8f, _wallMask);
        CurrentVelocity = velocity;
    }

    private void UpdateAlertLine()
    {
        if (_alertLine == null) return;

        if (_visibleWolves.Count == 0)
        {
            _alertLine.positionCount = 0;
            return;
        }

        _alertLine.positionCount = _visibleWolves.Count * 2;
        for (int i = 0; i < _visibleWolves.Count; i++)
        {
            _alertLine.SetPosition(i * 2, transform.position + Vector3.up * 0.3f);
            _alertLine.SetPosition(i * 2 + 1, _visibleWolves[i].transform.position + Vector3.up * 0.3f);
        }
    }

    #endregion
}
