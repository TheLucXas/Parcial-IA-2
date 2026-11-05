using System;
using System.Collections.Generic;
using UnityEngine;

public class Graph : MonoBehaviour
{
    #region Serialized Fields
    [Header("Generation")]
    [SerializeField] private Node _nodePrefab;
    [SerializeField] private Vector2 _areaSize = new(136f, 96f);
    [SerializeField] private float _cellSize = 4f;
    [SerializeField] private float _clearanceRadius = 0.75f;
    [SerializeField] private LayerMask _wallMask;

    [Header("Nodes")]
    [SerializeField] private List<Node> _nodes = new();

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _connectionsColor = new(1f, 1f, 1f, 0.15f);
    #endregion

    #region Private Fields
    private readonly List<(float sqrDistance, Node node)> _closestBuffer = new();
    #endregion

    #region Properties
    public static Graph Current { get; private set; }
    public IReadOnlyList<Node> Nodes => _nodes;
    public LayerMask WallMask => _wallMask;
    public bool DrawGizmos => _drawGizmos;
    public Color ConnectionsColor => _connectionsColor;
    #endregion

    #region Unity Callbacks
    private void Awake() => Current = this;

    private void OnValidate() => Current = this;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, new Vector3(_areaSize.x, 0f, _areaSize.y));
    }
    #endregion

    #region Public Methods
    public Node GetClosestNode(Vector3 position, Func<Node, bool> filter = null)
    {
        _closestBuffer.Clear();
        Node closest = null;
        float closestSqr = float.MaxValue;
        float searchSqr = _cellSize * _cellSize * 4f;

        foreach (Node node in _nodes)
        {
            if (filter != null && !filter(node)) continue;

            float sqr = (node.Position - position).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = node;
            }
            if (sqr <= searchSqr) _closestBuffer.Add((sqr, node));
        }

        _closestBuffer.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));
        foreach (var item in _closestBuffer)
        {
            if (PerceptionUtils.InLineOfSight(position, item.node.Position, _wallMask)) return item.node;
        }

        return closest;
    }

    public Node GetRandomNode(ZoneType zone, Func<Node, bool> filter = null)
    {
        _closestBuffer.Clear();
        foreach (Node node in _nodes)
        {
            if (node.Zone != zone) continue;
            if (filter != null && !filter(node)) continue;
            _closestBuffer.Add((0f, node));
        }

        if (_closestBuffer.Count == 0) return null;
        return _closestBuffer[UnityEngine.Random.Range(0, _closestBuffer.Count)].node;
    }

    public Node GetRandomNodeNear(Vector3 position, float radius, Func<Node, bool> filter = null)
    {
        _closestBuffer.Clear();
        float sqrRadius = radius * radius;
        foreach (Node node in _nodes)
        {
            if (filter != null && !filter(node)) continue;
            if ((node.Position - position).sqrMagnitude <= sqrRadius) _closestBuffer.Add((0f, node));
        }

        if (_closestBuffer.Count == 0) return null;
        return _closestBuffer[UnityEngine.Random.Range(0, _closestBuffer.Count)].node;
    }

    [ContextMenu("Generate Graph")]
    public void GenerateGraph()
    {
        ClearGraph();
        Physics.SyncTransforms();

        int columns = Mathf.RoundToInt(_areaSize.x / _cellSize);
        int rows = Mathf.RoundToInt(_areaSize.y / _cellSize);
        Node[,] grid = new Node[columns, rows];
        Vector3 origin = transform.position - new Vector3(_areaSize.x, 0f, _areaSize.y) * 0.5f;
        Vector3 halfCell = new Vector3(_cellSize, 1f, _cellSize) * 0.45f;
        MapZone[] zones = FindObjectsByType<MapZone>(FindObjectsSortMode.None);

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector3 position = origin + new Vector3((x + 0.5f) * _cellSize, 0f, (y + 0.5f) * _cellSize);
                if (Physics.CheckBox(position + Vector3.up, halfCell, Quaternion.identity, _wallMask)) continue;

                Node node = CreateNode(position);
                node.name = $"Node_{x}_{y}";
                node.Setup(GetZoneAt(zones, position));
                grid[x, y] = node;
                _nodes.Add(node);
            }
        }

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Node node = grid[x, y];
                if (node == null) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= columns || ny >= rows) continue;

                        Node neighbor = grid[nx, ny];
                        if (neighbor == null) continue;
                        if (PerceptionUtils.IsPathClear(node.Position, neighbor.Position, _clearanceRadius, _wallMask)) node.AddNeighbor(neighbor);
                    }
                }
            }
        }
    }

    [ContextMenu("Clear Graph")]
    public void ClearGraph()
    {
        foreach (Node node in _nodes)
        {
            if (node == null) continue;
            if (Application.isPlaying) Destroy(node.gameObject);
            else DestroyImmediate(node.gameObject);
        }
        _nodes.Clear();
    }
    #endregion

    #region Private Methods
    private Node CreateNode(Vector3 position)
    {
        Node node;
        if (_nodePrefab != null)
        {
            node = Instantiate(_nodePrefab, position, Quaternion.identity, transform);
        }
        else
        {
            GameObject nodeObject = new("Node");
            nodeObject.transform.SetParent(transform);
            nodeObject.transform.position = position;
            node = nodeObject.AddComponent<Node>();
        }
        return node;
    }

    private static ZoneType GetZoneAt(MapZone[] zones, Vector3 position)
    {
        foreach (MapZone zone in zones)
        {
            if (zone.Contains(position)) return zone.Type;
        }
        return ZoneType.Corridor;
    }
    #endregion
}
