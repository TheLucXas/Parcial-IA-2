using UnityEngine;

public class ExitState : HunterState
{
    #region Events
    public static event System.Action<HunterAgent> OnExitReached;
    #endregion

    #region Private Fields
    private readonly ExitData _data;
    #endregion

    #region Constructor
    public ExitState(ExitData data, HunterAgent agent) : base(agent)
    {
        _data = data;
    }
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.CurrentTarget = null;
        _agent.UpdateStateText("Exit (A*)");

        MapZone exitZone = MapZone.GetFirst(ZoneType.Exit);
        if (exitZone != null && exitZone.CenterNode != null) _agent.SetDestination(exitZone.CenterNode.Position);
    }

    public override void Update()
    {
        if (TryChangeToDead()) return;
        if (TryChangeToFlee()) return;

        if (!_agent.FollowPath(_data.Speed, _data.SteerForce)) return;

        _agent.RegisterExit();
        OnExitReached?.Invoke(_agent);
        _agent.ResetAtStart();
        _fsm.ChangeState(HunterStates.Search);
    }

    public override void Exit()
    {
        _agent.StopMoving();
    }
    #endregion
}

[System.Serializable]
public class ExitData
{
    [Header("Movement")]
    public float Speed = 12f;
    public float SteerForce = 60f;
}
