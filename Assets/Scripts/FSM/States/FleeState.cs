using System.Collections.Generic;
using UnityEngine;

public class FleeState : HunterState
{
    #region Events
    public static event System.Action<MapZone, float> OnSafeZoneChosen;
    public static event System.Action<MapZone> OnSafeZoneWaitStarted;
    #endregion

    #region Private Fields
    private readonly FleeData _data;
    private readonly List<MapZone> _safeZones = new();
    private bool _isWaiting;
    private float _timer;
    #endregion

    #region Constructor
    public FleeState(FleeData data, HunterAgent agent) : base(agent)
    {
        _data = data;
    }
    #endregion

    #region State Overrides
    public override void Enter()
    {
        _isWaiting = false;
        _timer = 0f;
        _agent.CurrentTarget = null;
        ChooseSafeZone();
    }

    public override void Update()
    {
        if (TryChangeToDead()) return;

        if (_isWaiting)
        {
            _timer += Time.deltaTime;
            _agent.UpdateStateText($"Flee: wait {_timer:0.0}/{_agent.SafeZoneWaitTime:0.0}s");
            if (_timer >= _agent.SafeZoneWaitTime) ResumeObjective();
            return;
        }

        MapZone zone = _agent.ChosenSafeZone;
        if (zone == null)
        {
            ChooseSafeZone();
            return;
        }

        bool arrived = _agent.FollowPath(_data.Speed, _data.SteerForce);
        if (arrived && zone.Contains(_agent.transform.position))
        {
            _isWaiting = true;
            _agent.StopMoving();
            OnSafeZoneWaitStarted?.Invoke(zone);
        }
        else if (arrived)
        {
            ChooseSafeZone();
        }
    }

    public override void Exit()
    {
        _agent.StopMoving();
        _agent.ChosenSafeZone = null;
        _isWaiting = false;
    }
    #endregion

    #region Private Methods
    private void ChooseSafeZone()
    {
        MapZone.GetZones(ZoneType.SafeZone, _safeZones);
        if (Graph.Current == null || _safeZones.Count == 0) return;

        Node startNode = Graph.Current.GetClosestNode(_agent.transform.position);
        MapZone bestZone = null;
        List<Node> bestPath = null;
        float bestLength = float.MaxValue;

        foreach (MapZone zone in _safeZones)
        {
            List<Node> path = Pathfinding.AStar(startNode, zone.CenterNode);
            if (path.Count == 0) continue;

            float length = Vector3.Distance(_agent.transform.position, path[0].Position) + Pathfinding.GetPathLength(path);
            if (length < bestLength)
            {
                bestLength = length;
                bestZone = zone;
                bestPath = path;
            }
        }

        _agent.ChosenSafeZone = bestZone;
        if (bestZone == null) return;

        _agent.PathFollower.SetPath(bestPath, bestZone.CenterNode.Position);
        _agent.UpdateStateText($"Flee -> {bestZone.DisplayName.Split('(')[0].Trim()} (A*)");
        OnSafeZoneChosen?.Invoke(bestZone, bestLength);
    }
    #endregion
}

[System.Serializable]
public class FleeData
{
    [Header("Movement")]
    public float Speed = 13f;
    public float SteerForce = 70f;
}
