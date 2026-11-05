public abstract class HunterState : State<HunterStates>
{
    #region Protected Fields
    protected readonly HunterAgent _agent;
    #endregion

    #region Constructor
    protected HunterState(HunterAgent agent) => _agent = agent;
    #endregion

    #region Protected Methods
    protected bool TryChangeToDead()
    {
        if (!_agent.IsCaught) return false;
        _fsm.ChangeState(HunterStates.Dead);
        return true;
    }

    protected bool TryChangeToFlee()
    {
        if (!_agent.SeesWolf) return false;
        _fsm.ChangeState(HunterStates.Flee);
        return true;
    }

    protected bool TryChangeToGather()
    {
        Agent target = _agent.GetClosest(_agent.VisibleDeadBoids, true);
        if (target == null) return false;
        _agent.CurrentTarget = target;
        _fsm.ChangeState(HunterStates.Gather);
        return true;
    }

    protected bool TryChangeToAttack()
    {
        if (_agent.AttackCooldownTimer > 0f) return false;
        Agent target = _agent.GetClosest(_agent.VisibleBoids, false);
        if (target == null) return false;
        _agent.CurrentTarget = target;
        _fsm.ChangeState(HunterStates.Attack);
        return true;
    }

    protected void ResumeObjective()
    {
        _fsm.ChangeState(_agent.Gathers >= _agent.RequiredGathers ? HunterStates.Exit : HunterStates.Search);
    }
    #endregion
}
