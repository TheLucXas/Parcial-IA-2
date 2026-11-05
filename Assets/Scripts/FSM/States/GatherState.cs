using UnityEngine;

public class GatherState : HunterState
{
    #region Events
    public static event System.Action<bool> OnGatherFinished;
    #endregion

    #region Private Fields
    private readonly GatherData _data;
    private Agent _targetAgent;
    private bool _isGathering;
    private bool _completed;
    private float _timer;
    #endregion

    #region Constructor
    public GatherState(GatherData data, HunterAgent agent) : base(agent)
    {
        _data = data;
    }
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.UpdateStateText("Gather");
        _targetAgent = _agent.CurrentTarget;
        _isGathering = false;
        _completed = false;
        _timer = 0f;
    }

    public override void Update()
    {
        if (TryChangeToDead()) return;
        if (TryChangeToFlee()) return;

        if (_targetAgent == null || !_targetAgent.IsDead || !_targetAgent.IsAvailable || !_agent.IsDeadBoidVisible(_targetAgent))
        {
            _fsm.ChangeState(HunterStates.Search);
            return;
        }

        if (_isGathering)
        {
            _timer += Time.deltaTime;
            _agent.UpdateStateText($"Gather {_timer:0.0}/{_data.GatherTime:0.0}s");

            if (_timer >= _data.GatherTime)
            {
                _targetAgent.Collect();
                _agent.AddGather();
                _completed = true;
                _fsm.ChangeState(_agent.Gathers >= _agent.RequiredGathers ? HunterStates.Exit : HunterStates.Search);
            }
            return;
        }

        Vector3 agentPos2D = PerceptionUtils.Flat(_agent.transform.position);
        Vector3 targetPos2D = PerceptionUtils.Flat(_targetAgent.transform.position);

        if (Vector3.Distance(agentPos2D, targetPos2D) <= _data.GatherCheckDistance)
        {
            _isGathering = true;
            _agent.CurrentVelocity = Vector3.zero;
            return;
        }

        _agent.ArriveTo(targetPos2D, _data.Speed, _data.SteerForce, _data.ArriveRadius);
    }

    public override void Exit()
    {
        OnGatherFinished?.Invoke(_completed);

        _targetAgent = null;
        _agent.CurrentTarget = null;
        _isGathering = false;
    }
    #endregion
}

[System.Serializable]
public class GatherData
{
    [Header("Timing")]
    public float GatherTime = 3f;

    [Header("Movement")]
    public float Speed = 11f;
    public float SteerForce = 20f;
    public float ArriveRadius = 1.75f;
    public float GatherCheckDistance = 1.25f;
}
