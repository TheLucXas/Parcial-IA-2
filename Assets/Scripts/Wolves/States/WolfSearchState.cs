using UnityEngine;

public class WolfSearchState : State<WolfStates>
{
    #region Private Fields
    private readonly WolfAgent _agent;
    private float _timer;
    private float _lookTimer;
    #endregion

    #region Constructor
    public WolfSearchState(WolfAgent agent) => _agent = agent;
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _timer = 0f;
        _lookTimer = 1f;
        _agent.StopMoving();
        _agent.UpdateStateText("Search");
    }

    public override void Update()
    {
        if (!_agent.HasLastKnownPosition)
        {
            _fsm.ChangeState(WolfStates.Patrol);
            return;
        }

        if (_agent.CanSeeHunter)
        {
            _fsm.ChangeState(WolfStates.Chase);
            return;
        }

        if (_agent.HasPendingAlert)
        {
            _fsm.ChangeState(WolfStates.Investigate);
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _agent.SearchDuration)
        {
            _fsm.ChangeState(WolfStates.Patrol);
            return;
        }

        if (_lookTimer > 0f)
        {
            _lookTimer -= Time.deltaTime;
            _agent.LookAround(180f);
            if (_lookTimer <= 0f) PickSearchPoint();
            return;
        }

        if (_agent.FollowPath(_agent.SearchSpeed)) _lookTimer = 0.8f;
    }

    public override void Exit()
    {
        _agent.StopMoving();
    }
    #endregion

    #region Private Methods
    private void PickSearchPoint()
    {
        Node node = Graph.Current != null ? Graph.Current.GetRandomNodeNear(_agent.LastKnownPosition, _agent.SearchRadius, WolfAgent.IsTransitable) : null;
        if (node == null || !_agent.MoveTo(node.Position, false))
        {
            _lookTimer = 0.8f;
            return;
        }
        _agent.UpdateStateText(_agent.PathFollower.IsUsingPathfinding ? "Search (A*)" : "Search");
    }
    #endregion
}
