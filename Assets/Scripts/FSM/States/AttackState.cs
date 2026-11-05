using UnityEngine;

public class AttackState : HunterState
{
    #region Events
    public static event System.Action<bool> OnAttackPerformed;
    #endregion

    #region Private Fields
    private readonly AttackData _data;

    private Agent _targetAgent;

    private float _rangedWaitTimer = 0f;
    #endregion

    #region Constructor
    public AttackState(AttackData data, HunterAgent agent) : base(agent)
    {
        _data = data;
    }
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.UpdateStateText("Attack");
        _targetAgent = _agent.CurrentTarget;
        _rangedWaitTimer = 0f;
    }

    public override void Update()
    {
        if (TryChangeToDead()) return;
        if (TryChangeToFlee()) return;
        if (TryChangeToGather()) return;

        if (_targetAgent == null || _targetAgent.IsDead || !_agent.IsBoidVisible(_targetAgent))
        {
            _fsm.ChangeState(HunterStates.Search);
            return;
        }

        float dist = Vector3.Distance(PerceptionUtils.Flat(_targetAgent.transform.position), PerceptionUtils.Flat(_agent.transform.position));

        if (dist <= _data.MeleeAttackRadius)
        {
            _agent.UpdateStateText("Attack (Melee)");
            _targetAgent.TakeDamage(_data.AttackDamage * 2f);

            ShootCard(0f, true);
            FinishAttack(true);
            return;
        }

        if (dist <= _data.RangeAttackRadius)
        {
            _agent.UpdateStateText("Attack (Range)");
            _rangedWaitTimer += Time.deltaTime;

            if (_rangedWaitTimer >= _data.RangedAttackDelay)
            {
                ShootCard(_data.AttackDamage, false);
                FinishAttack(false);
                return;
            }
        }
        else
        {
            _agent.UpdateStateText("Attack (Pursuit)");
            _rangedWaitTimer = 0f;
        }

        _agent.PursuitTarget(_targetAgent, _data.ChaseSpeed, _data.ChaseForce);
        if (Bounds.Instance != null) _agent.transform.position = Bounds.Instance.ClampPosition(_agent.transform.position);
    }

    public override void Exit()
    {
        _targetAgent = null;
        _agent.CurrentTarget = null;
    }
    #endregion

    #region Private Methods
    private void FinishAttack(bool isMelee)
    {
        _agent.AttackCooldownTimer = _data.TimeBetweenAttacks;
        OnAttackPerformed?.Invoke(isMelee);
        _fsm.ChangeState(HunterStates.Search);
    }

    private void ShootCard(float damage, bool isMelee)
    {
        if (_data.ProjectilePrefab != null)
        {
            Vector3 spawnPos = _agent.transform.position + (Vector3.up * 1f);
            Projectile newProjectile = Object.Instantiate(_data.ProjectilePrefab, spawnPos, Quaternion.identity);

            newProjectile.Initialize(_targetAgent, damage, isMelee, _data.MeleeAttackRadius);
        }
    }
    #endregion
}

[System.Serializable]
public class AttackData
{
    [Header("Damage")]
    public float AttackDamage = 1f;

    [Header("Radii")]
    public float MeleeAttackRadius = 3f;
    public float RangeAttackRadius = 6f;

    [Header("Timing")]
    public float TimeBetweenAttacks = 5f;
    public float RangedAttackDelay = 1.5f;

    [Header("Chase")]
    public float ChaseSpeed = 14f;
    public float ChaseForce = 14f;

    [Header("Projectile")]
    public Projectile ProjectilePrefab;
}
