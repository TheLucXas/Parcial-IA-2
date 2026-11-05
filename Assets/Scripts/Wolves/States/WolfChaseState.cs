using UnityEngine;

public class WolfChaseState : State<WolfStates>
{
    #region Private Fields
    private readonly WolfAgent _agent;
    private float _replanTimer;
    #endregion

    #region Constructor
    public WolfChaseState(WolfAgent agent) => _agent = agent;
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.StopMoving();
        _agent.UpdateStateText("Chase! (Shout)");
        _agent.Howl();
        _replanTimer = 0f;
    }

    public override void Update()
    {
        if (!_agent.HasLastKnownPosition)
        {
            _fsm.ChangeState(WolfStates.Patrol);
            return;
        }

        if (!_agent.CanSeeHunter)
        {
            _fsm.ChangeState(WolfStates.Investigate);
            return;
        }

        Vector3 hunterPosition = _agent.VisibleHunter.transform.position;
        if (_agent.IsDirectPathClear(hunterPosition))
        {
            _agent.PathFollower.Clear();
            _agent.UpdateStateText("Chase!");
            _agent.PursueHunter(_agent.ChaseSpeed);
            return;
        }

        _replanTimer -= Time.deltaTime;
        if (_replanTimer <= 0f || !_agent.PathFollower.HasDestination)
        {
            _replanTimer = 0.5f;
            _agent.MoveTo(hunterPosition, false);
            _agent.UpdateStateText("Chase! (A*)");
        }
        _agent.FollowPath(_agent.ChaseSpeed);
    }

    public override void Exit()
    {
        _agent.StopMoving();
    }
    #endregion
}
