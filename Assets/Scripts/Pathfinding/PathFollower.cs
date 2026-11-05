using System;
using System.Collections.Generic;
using UnityEngine;

public class PathFollower
{
    #region Private Fields
    private readonly List<Node> _path = new();
    private int _index;
    private Vector3 _destination;
    private bool _isDirect;
    private float _shortcutTimer;

    private readonly float _clearanceRadius;
    private readonly LayerMask _wallMask;
    #endregion

    #region Properties
    public bool HasDestination { get; private set; }
    public bool IsUsingPathfinding => HasDestination && !_isDirect;
    #endregion

    #region Constructor
    public PathFollower(float clearanceRadius, LayerMask wallMask)
    {
        _clearanceRadius = clearanceRadius;
        _wallMask = wallMask;
    }
    #endregion

    #region Public Methods
    public bool SetDestination(Vector3 from, Vector3 destination, Func<Node, bool> isTransitable = null, bool allowDirect = true)
    {
        Clear();

        if (allowDirect && PerceptionUtils.IsPathClear(from, destination, _clearanceRadius, _wallMask))
        {
            _destination = PerceptionUtils.Flat(destination);
            _isDirect = true;
            HasDestination = true;
            return true;
        }

        if (Graph.Current == null) return false;

        Node startNode = Graph.Current.GetClosestNode(from, isTransitable);
        Node endNode = Graph.Current.GetClosestNode(destination, isTransitable);
        List<Node> path = Pathfinding.AStar(startNode, endNode, isTransitable);

        return SetPath(path, destination);
    }

    public bool SetPath(List<Node> path, Vector3 destination)
    {
        Clear();
        if (path == null || path.Count == 0) return false;

        _path.AddRange(path);
        _destination = PerceptionUtils.Flat(destination);
        _isDirect = false;
        HasDestination = true;
        return true;
    }

    public Vector3 GetSteeringTarget(Vector3 position, float reachDistance)
    {
        if (!_isDirect)
        {
            while (_index < _path.Count && PerceptionUtils.InRange(position, _path[_index].Position, reachDistance)) _index++;

            _shortcutTimer -= Time.deltaTime;
            if (_shortcutTimer <= 0f && _index + 1 < _path.Count)
            {
                _shortcutTimer = 0.2f;
                if (PerceptionUtils.IsPathClear(position, _path[_index + 1].Position, _clearanceRadius, _wallMask)) _index++;
            }

            if (_index < _path.Count) return _path[_index].Position;
        }

        return _destination;
    }

    public bool HasArrived(Vector3 position, float reachDistance)
    {
        if (!HasDestination) return false;
        if (!_isDirect && _index < _path.Count) return false;
        return PerceptionUtils.InRange(position, _destination, reachDistance);
    }

    public void Clear()
    {
        _path.Clear();
        _index = 0;
        _isDirect = false;
        _shortcutTimer = 0f;
        HasDestination = false;
    }

    public void UpdateLine(LineRenderer line, Vector3 position)
    {
        if (line == null) return;

        if (!HasDestination)
        {
            line.positionCount = 0;
            return;
        }

        int remaining = _isDirect ? 0 : Mathf.Max(0, _path.Count - _index);
        line.positionCount = remaining + 2;
        line.SetPosition(0, Lift(position));
        for (int i = 0; i < remaining; i++)
        {
            line.SetPosition(i + 1, Lift(_path[_index + i].Position));
        }
        line.SetPosition(remaining + 1, Lift(_destination));
    }

    public void DrawGizmos(Vector3 position, Color color)
    {
        if (!HasDestination) return;

        Gizmos.color = color;
        Vector3 previous = position;
        if (!_isDirect)
        {
            for (int i = _index; i < _path.Count; i++)
            {
                Gizmos.DrawLine(previous, _path[i].Position);
                Gizmos.DrawWireSphere(_path[i].Position, 0.35f);
                previous = _path[i].Position;
            }
        }
        Gizmos.DrawLine(previous, _destination);
        Gizmos.DrawWireCube(_destination, Vector3.one * 0.8f);
    }
    #endregion

    #region Private Methods
    private static Vector3 Lift(Vector3 position) => new(position.x, 0.2f, position.z);
    #endregion
}
