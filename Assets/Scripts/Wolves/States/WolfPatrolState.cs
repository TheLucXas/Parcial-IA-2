using UnityEngine;

public class WolfPatrolState : State<WolfStates>
{
    #region Private Fields
    private readonly WolfAgent _agent;
    #endregion

    #region Constructor
    public WolfPatrolState(WolfAgent agent) => _agent = agent;
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.ForgetTarget();
        GoToCurrentWaypoint();
    }

    public override void Update()
    {
        if (_agent.CanSeeHunter)
        {
            _fsm.ChangeState(WolfStates.Chase);
            return;
        }

        if (_agent.ConsumeAlert())
        {
            _fsm.ChangeState(WolfStates.Investigate);
            return;
        }

        if (_agent.Waypoints.Count == 0) return;

        if (_agent.FollowPath(_agent.PatrolSpeed))
        {
            AdvanceWaypoint();
            GoToCurrentWaypoint();
        }
    }

    public override void Exit()
    {
        _agent.StopMoving();
    }
    #endregion

    #region Private Methods
    private void GoToCurrentWaypoint()
    {
        if (_agent.Waypoints.Count == 0)
        {
            _agent.UpdateStateText("Patrol");
            return;
        }

        Transform waypoint = _agent.Waypoints[_agent.WaypointIndex];
        _agent.MoveTo(waypoint.position);
        _agent.UpdateStateText(_agent.PathFollower.IsUsingPathfinding ? "Patrol (A*)" : "Patrol");
    }

    private void AdvanceWaypoint()
    {
        int count = _agent.Waypoints.Count;
        if (count < 2) return;

        if (_agent.PatrolMode == PatrolMode.Loop)
        {
            _agent.WaypointIndex = (_agent.WaypointIndex + 1) % count;
            return;
        }

        int next = _agent.WaypointIndex + _agent.PatrolDirection;
        if (next < 0 || next >= count)
        {
            _agent.PatrolDirection = -_agent.PatrolDirection;
            next = _agent.WaypointIndex + _agent.PatrolDirection;
        }
        _agent.WaypointIndex = next;
    }
    #endregion
}
