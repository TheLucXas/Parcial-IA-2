using UnityEngine;

public class SearchState : HunterState
{
    #region Private Fields
    private readonly SearchData _data;
    private float _timer;
    private float _ballSpawnTime;
    private bool _wasInsideCoto;
    #endregion

    #region Constructor
    public SearchState(SearchData data, HunterAgent agent) : base(agent)
    {
        _data = data;
    }
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _agent.CurrentTarget = null;
        _timer = 0f;
        _ballSpawnTime = Random.Range(_data.minBallSpawnTime, _data.maxBallSpawnTime);
        _wasInsideCoto = _agent.IsInsideCoto;
        PickNextDestination();
    }

    public override void Update()
    {
        if (TryChangeToDead()) return;
        if (TryChangeToFlee()) return;
        if (TryChangeToGather()) return;
        if (TryChangeToAttack()) return;

        bool insideCoto = _agent.IsInsideCoto;
        if (insideCoto != _wasInsideCoto)
        {
            _wasInsideCoto = insideCoto;
            UpdateLabel();
        }

        if (_agent.FollowPath(_data.speed, _data.steerForce)) PickNextDestination();

        if (!insideCoto) return;

        _timer += Time.deltaTime;
        if (_timer >= _ballSpawnTime && Ball.ActiveCount < _data.maxBallsCount)
        {
            var pos = new Vector3(_agent.transform.position.x, 0f, _agent.transform.position.z);
            Object.Instantiate(_data.ball, pos, Quaternion.identity);
            _ballSpawnTime = Random.Range(_data.minBallSpawnTime, _data.maxBallSpawnTime);
            _timer = 0f;
        }
    }

    public override void Exit()
    {
        _agent.StopMoving();
    }
    #endregion

    #region Private Methods
    private void PickNextDestination()
    {
        Node target = Graph.Current != null ? Graph.Current.GetRandomNode(ZoneType.Coto) : null;
        if (target != null) _agent.SetDestination(target.Position);
        UpdateLabel();
    }

    private void UpdateLabel() => _agent.UpdateStateText(_agent.IsInsideCoto ? "Search (A*)" : "Search -> Coto (A*)");
    #endregion
}

[System.Serializable]
public class SearchData
{
    [Header("Movement")]
    public float speed = 11f;
    public float steerForce = 60f;

    [Header("Ball Spawning")]
    public GameObject ball;
    public float minBallSpawnTime = 3f;
    public float maxBallSpawnTime = 9f;
    public int maxBallsCount = 5;
}
