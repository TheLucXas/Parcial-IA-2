public class WolfInvestigateState : State<WolfStates>
{
    #region Private Fields
    private readonly WolfAgent _agent;
    #endregion

    #region Constructor
    public WolfInvestigateState(WolfAgent agent) => _agent = agent;
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.ConsumeAlert();
        GoToLastKnownPosition();
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

        if (_agent.ConsumeAlert()) GoToLastKnownPosition();

        if (_agent.FollowPath(_agent.InvestigateSpeed)) _fsm.ChangeState(WolfStates.Search);
    }

    public override void Exit()
    {
        _agent.StopMoving();
    }
    #endregion

    #region Private Methods
    private void GoToLastKnownPosition()
    {
        if (!_agent.MoveTo(_agent.LastKnownPosition, false))
        {
            _agent.StopMoving();
            return;
        }
        _agent.UpdateStateText(_agent.PathFollower.IsUsingPathfinding ? "Investigate (A*)" : "Investigate");
    }
    #endregion
}
