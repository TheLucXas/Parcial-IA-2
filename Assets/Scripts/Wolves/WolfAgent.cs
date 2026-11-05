using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum WolfStates { Patrol, Chase, Investigate, Search }

public enum PatrolMode { Loop, PingPong }

public class WolfAgent : MonoBehaviour
{
    #region Events
    public static event Action<WolfAgent, Vector3> OnHowl;
    public static event Action<WolfAgent, Vector3> OnAlertReceived;
    #endregion

    #region Serialized Fields
    [Header("Patrol")]
    [SerializeField] private List<Transform> _waypoints = new();
    [SerializeField] private PatrolMode _patrolMode = PatrolMode.Loop;

    [Header("Speeds")]
    [SerializeField] private float _patrolSpeed = 5f;
    [SerializeField] private float _investigateSpeed = 7f;
    [SerializeField] private float _searchSpeed = 5.5f;
    [SerializeField] private float _chaseSpeed = 9f;
    [SerializeField] private float _steerForce = 45f;

    [Header("Field of View")]
    [SerializeField] private float _viewRadius = 14f;
    [SerializeField, Range(10f, 360f)] private float _viewAngle = 100f;
    [SerializeField] private float _perceptionInterval = 0.1f;
    [SerializeField] private TriggerSensor _sensor;
    [SerializeField] private LayerMask _wallMask;

    [Header("Hunt")]
    [SerializeField] private float _captureRadius = 1.6f;
    [SerializeField] private float _howlCooldown = 2f;
    [SerializeField] private float _searchDuration = 7f;
    [SerializeField] private float _searchRadius = 10f;

    [Header("Navigation")]
    [SerializeField] private float _bodyRadius = 0.6f;
    [SerializeField] private float _nodeReachDistance = 1.5f;

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _fovColor = new(1f, 0.6f, 0.1f);
    [SerializeField] private Color _pathColor = new(1f, 0.35f, 0.1f);

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private SpriteRenderer _markerRenderer;
    [SerializeField] private TextMeshProUGUI _stateText;
    [SerializeField] private LineRenderer _pathLine;
    [SerializeField] private LineRenderer _fovLine;
    [SerializeField] private LineRenderer _howlLine;
    [SerializeField] private LineRenderer _lastKnownLine;
    [SerializeField] private Color _patrolColor = new(0.8f, 0.88f, 1f);
    [SerializeField] private Color _alertColor = new(1f, 0.85f, 0.2f);
    [SerializeField] private Color _chaseColor = new(1f, 0.2f, 0.15f);
    #endregion

    #region Private Fields
    private readonly FiniteStateMachine<WolfStates> _fsm = new();
    private static readonly List<WolfAgent> _allWolves = new();

    private PathFollower _pathFollower;
    private HunterAgent _nearbyHunter;
    private float _perceptionTimer;
    private float _howlCooldownTimer;
    private float _howlVisualTimer;
    private float _alertVisualTimer;
    private Vector3 _velocity;
    private string _stateLabel = "";
    private bool _canSeeHunter;
    private HunterAgent _visibleHunter;
    #endregion

    #region Properties
    public static IReadOnlyList<WolfAgent> AllWolves => _allWolves;
    public WolfStates CurrentState => _fsm.CurrentStateID;
    public IReadOnlyList<Transform> Waypoints => _waypoints;
    public PatrolMode PatrolMode => _patrolMode;
    public float PatrolSpeed => _patrolSpeed;
    public float InvestigateSpeed => _investigateSpeed;
    public float SearchSpeed => _searchSpeed;
    public float ChaseSpeed => _chaseSpeed;
    public float SearchDuration => _searchDuration;
    public float SearchRadius => _searchRadius;
    public PathFollower PathFollower => _pathFollower;
    public string StateLabel => _stateLabel;

    public bool CanSeeHunter => _canSeeHunter && _visibleHunter != null && _visibleHunter.IsVisible && !_visibleHunter.IsInSafeZone;
    public HunterAgent VisibleHunter => CanSeeHunter ? _visibleHunter : null;
    public bool HasLastKnownPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public bool HasPendingAlert { get; private set; }
    public int WaypointIndex { get; set; }
    public int PatrolDirection { get; set; } = 1;

    public static bool IsTransitable(Node node) => node.Zone != ZoneType.SafeZone;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _pathFollower = new PathFollower(_bodyRadius, _wallMask);
    }

    private void OnEnable()
    {
        _allWolves.Add(this);
        OnHowl += HandleHowl;
        MapZone.OnHunterEnteredSafeZone += HandleHunterSafe;
        HunterAgent.OnCaught += HandleHunterCaught;

        if (_sensor == null) return;
        _sensor.SetRadius(_viewRadius);
        _sensor.OnEnter += HandleSensorEnter;
        _sensor.OnExit += HandleSensorExit;
    }

    private void OnDisable()
    {
        _allWolves.Remove(this);
        OnHowl -= HandleHowl;
        MapZone.OnHunterEnteredSafeZone -= HandleHunterSafe;
        HunterAgent.OnCaught -= HandleHunterCaught;

        if (_sensor == null) return;
        _sensor.OnEnter -= HandleSensorEnter;
        _sensor.OnExit -= HandleSensorExit;
    }

    private void Start()
    {
        _fsm.AddState(WolfStates.Patrol, new WolfPatrolState(this));
        _fsm.AddState(WolfStates.Chase, new WolfChaseState(this));
        _fsm.AddState(WolfStates.Investigate, new WolfInvestigateState(this));
        _fsm.AddState(WolfStates.Search, new WolfSearchState(this));
        _fsm.ChangeState(WolfStates.Patrol);
    }

    private void Update()
    {
        if (_howlCooldownTimer > 0f) _howlCooldownTimer -= Time.deltaTime;
        if (_howlVisualTimer > 0f) _howlVisualTimer -= Time.deltaTime;
        if (_alertVisualTimer > 0f) _alertVisualTimer -= Time.deltaTime;

        _perceptionTimer -= Time.deltaTime;
        if (_perceptionTimer <= 0f)
        {
            _perceptionTimer = _perceptionInterval;
            UpdatePerception();
        }

        TryCatchHunter();
        _fsm.Update();

        UpdateFeedback();
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Vector3 pos = PerceptionUtils.Flat(transform.position);
        Gizmos.color = CanSeeHunter ? Color.red : _fovColor;
        DrawFovGizmo(pos);

        Gizmos.color = Color.red;
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, _captureRadius);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            foreach (Transform waypoint in _waypoints)
            {
                if (waypoint != null) Gizmos.DrawLine(pos, waypoint.position);
            }
            return;
        }

        _pathFollower.DrawGizmos(pos, _pathColor);

        if (HasLastKnownPosition)
        {
            Gizmos.color = _alertColor;
            Gizmos.DrawLine(pos, LastKnownPosition);
            Gizmos.DrawWireSphere(LastKnownPosition, 1f);
        }

        if (CanSeeHunter && VisibleHunter != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, VisibleHunter.transform.position);
        }
    }
    #endregion

    #region Public Methods
    public void UpdateStateText(string text)
    {
        _stateLabel = text;
        if (_stateText != null) _stateText.text = $"{name}\n{text}";
    }

    public void Howl()
    {
        if (_howlCooldownTimer > 0f || !HasLastKnownPosition) return;

        _howlCooldownTimer = _howlCooldown;
        _howlVisualTimer = 1f;
        OnHowl?.Invoke(this, LastKnownPosition);
    }

    public bool ConsumeAlert()
    {
        if (!HasPendingAlert) return false;
        HasPendingAlert = false;
        return true;
    }

    public void ForgetTarget()
    {
        _canSeeHunter = false;
        _visibleHunter = null;
        HasLastKnownPosition = false;
        HasPendingAlert = false;
    }

    public bool MoveTo(Vector3 destination, bool allowDirect = true) => _pathFollower.SetDestination(transform.position, destination, IsTransitable, allowDirect);

    public bool FollowPath(float speed)
    {
        if (!_pathFollower.HasDestination) return true;
        if (_pathFollower.HasArrived(transform.position, _nodeReachDistance))
        {
            _velocity = Vector3.zero;
            return true;
        }

        Vector3 target = _pathFollower.GetSteeringTarget(transform.position, _nodeReachDistance);
        Steer(SteeringUtils.CalculateSeek(transform.position, _velocity, target, speed, _steerForce));
        return false;
    }

    public void PursueHunter(float speed)
    {
        if (VisibleHunter == null) return;
        Vector3 future = SteeringUtils.GetFuturePosition(transform.position, speed, VisibleHunter.transform.position, VisibleHunter.CurrentVelocity);
        if (!PerceptionUtils.IsPathClear(transform.position, future, _bodyRadius, _wallMask)) future = VisibleHunter.transform.position;
        Steer(SteeringUtils.CalculateSeek(transform.position, _velocity, PerceptionUtils.Flat(future), speed, _steerForce));
    }

    public bool IsDirectPathClear(Vector3 target) => PerceptionUtils.IsPathClear(transform.position, target, _bodyRadius, _wallMask);

    public void LookAround(float degreesPerSecond)
    {
        _velocity = Vector3.zero;
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime);
    }

    public void StopMoving()
    {
        _velocity = Vector3.zero;
        _pathFollower.Clear();
    }

    public bool TryCatchHunter()
    {
        HunterAgent hunter = _nearbyHunter;
        if (hunter == null || !hunter.IsVisible || hunter.IsInSafeZone) return false;
        if (!PerceptionUtils.InRange(transform.position, hunter.transform.position, _captureRadius)) return false;

        hunter.Catch();
        return true;
    }
    #endregion

    #region Private Methods
    private void UpdatePerception()
    {
        HunterAgent hunter = _nearbyHunter;
        bool canSee = hunter != null && hunter.IsVisible && !hunter.IsInSafeZone
            && PerceptionUtils.InFieldOfView(transform, hunter.transform.position, _viewRadius, _viewAngle, _wallMask);

        _canSeeHunter = canSee;
        _visibleHunter = canSee ? hunter : null;

        if (canSee)
        {
            LastKnownPosition = PerceptionUtils.Flat(hunter.transform.position);
            HasLastKnownPosition = true;
        }
    }

    private void Steer(Vector3 steering)
    {
        _velocity += steering;
        SteeringUtils.ApplyVelocity(transform, ref _velocity, _bodyRadius * 0.8f, _wallMask, IsForbiddenPosition);
    }

    private static bool IsForbiddenPosition(Vector3 position) => MapZone.IsInside(ZoneType.SafeZone, position);

    private void HandleHowl(WolfAgent sender, Vector3 position)
    {
        if (sender == this || CanSeeHunter) return;

        LastKnownPosition = position;
        HasLastKnownPosition = true;
        HasPendingAlert = true;
        _alertVisualTimer = 1.2f;
        OnAlertReceived?.Invoke(this, position);
    }

    private void HandleHunterSafe(MapZone zone, HunterAgent hunter) => ForgetTarget();

    private void HandleHunterCaught(HunterAgent hunter) => ForgetTarget();

    private void HandleSensorEnter(Collider other)
    {
        if (other.TryGetComponent(out HunterAgent hunter)) _nearbyHunter = hunter;
    }

    private void HandleSensorExit(Collider other)
    {
        if (other.TryGetComponent(out HunterAgent hunter) && hunter == _nearbyHunter) _nearbyHunter = null;
    }

    private Color GetStateColor() => CurrentState switch
    {
        WolfStates.Chase => _chaseColor,
        WolfStates.Investigate or WolfStates.Search => _alertColor,
        _ => _patrolColor,
    };

    private void UpdateFeedback()
    {
        Color stateColor = GetStateColor();
        bool flash = _alertVisualTimer > 0f && Mathf.Repeat(_alertVisualTimer, 0.3f) > 0.15f;
        if (_spriteRenderer != null) _spriteRenderer.color = flash ? Color.white : Color.Lerp(Color.white, stateColor, 0.35f);
        if (_markerRenderer != null) _markerRenderer.color = flash ? Color.white : new Color(stateColor.r, stateColor.g, stateColor.b, 0.85f);

        _pathFollower.UpdateLine(_pathLine, transform.position);

        if (_fovLine != null)
        {
            SetLineColor(_fovLine, CanSeeHunter ? _chaseColor : stateColor);
            const int segments = 16;
            _fovLine.positionCount = segments + 3;
            Vector3 origin = PerceptionUtils.Flat(transform.position) + Vector3.up * 0.15f;
            _fovLine.SetPosition(0, origin);
            for (int i = 0; i <= segments; i++)
            {
                float angle = -_viewAngle * 0.5f + _viewAngle * i / segments;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * PerceptionUtils.Flat(transform.forward).normalized;
                _fovLine.SetPosition(i + 1, origin + dir * _viewRadius);
            }
            _fovLine.SetPosition(segments + 2, origin);
        }

        if (_howlLine != null)
        {
            if (_howlVisualTimer > 0f)
            {
                const int segments = 24;
                float radius = Mathf.Lerp(1f, _viewRadius, 1f - _howlVisualTimer);
                _howlLine.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = Mathf.PI * 2f * i / segments;
                    _howlLine.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), 0.25f, Mathf.Sin(angle)) * radius);
                }
            }
            else _howlLine.positionCount = 0;
        }

        if (_lastKnownLine != null)
        {
            if (HasLastKnownPosition && !CanSeeHunter)
            {
                Vector3 lkp = LastKnownPosition + Vector3.up * 0.25f;
                _lastKnownLine.positionCount = 5;
                _lastKnownLine.SetPosition(0, lkp + new Vector3(-1f, 0f, -1f));
                _lastKnownLine.SetPosition(1, lkp + new Vector3(1f, 0f, 1f));
                _lastKnownLine.SetPosition(2, lkp);
                _lastKnownLine.SetPosition(3, lkp + new Vector3(-1f, 0f, 1f));
                _lastKnownLine.SetPosition(4, lkp + new Vector3(1f, 0f, -1f));
            }
            else _lastKnownLine.positionCount = 0;
        }
    }

    private static void SetLineColor(LineRenderer line, Color color)
    {
        line.startColor = color;
        line.endColor = color;
    }

    private void DrawFovGizmo(Vector3 pos)
    {
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, _viewRadius);
        Vector3 forward = PerceptionUtils.Flat(transform.forward).normalized;
        Vector3 left = Quaternion.AngleAxis(-_viewAngle * 0.5f, Vector3.up) * forward;
        Vector3 right = Quaternion.AngleAxis(_viewAngle * 0.5f, Vector3.up) * forward;
        Gizmos.DrawLine(pos, pos + left * _viewRadius);
        Gizmos.DrawLine(pos, pos + right * _viewRadius);

        const int segments = 12;
        Vector3 previous = pos + left * _viewRadius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(-_viewAngle * 0.5f + _viewAngle * i / segments, Vector3.up) * forward;
            Vector3 next = pos + dir * _viewRadius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
    #endregion
}
