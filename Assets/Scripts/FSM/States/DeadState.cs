using UnityEngine;

public class DeadState : HunterState
{
    #region Private Fields
    private float _timer;
    #endregion

    #region Constructor
    public DeadState(HunterAgent agent) : base(agent) { }
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _timer = 0f;
        _agent.RegisterDeath();
        _agent.SetVisible(false);
        _agent.UpdateStateText("Dead");
        _agent.NotifyCaught();
    }

    public override void Update()
    {
        _timer += Time.deltaTime;
        _agent.UpdateStateText($"Dead {Mathf.Max(0f, _agent.RespawnTime - _timer):0.0}s");

        if (_timer < _agent.RespawnTime) return;

        _agent.ResetAtStart();
        _fsm.ChangeState(HunterStates.Search);
    }

    public override void Exit()
    {
    }
    #endregion
}
