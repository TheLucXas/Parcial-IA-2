using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private ZoneType _zone = ZoneType.Corridor;
    [SerializeField] private List<Node> _neighbors = new();
    #endregion

    #region Properties
    public ZoneType Zone => _zone;
    public IReadOnlyList<Node> Neighbors => _neighbors;
    public Vector3 Position => transform.position;
    #endregion

    #region Unity Callbacks
    private void OnDrawGizmos()
    {
        if (Graph.Current == null || !Graph.Current.DrawGizmos) return;

        Gizmos.color = MapZone.GetZoneColor(_zone);
        Gizmos.DrawCube(transform.position, Vector3.one * 0.5f);

        Gizmos.color = Graph.Current.ConnectionsColor;
        foreach (Node neighbor in _neighbors)
        {
            if (neighbor != null) Gizmos.DrawLine(transform.position, neighbor.transform.position);
        }
    }
    #endregion

    #region Public Methods
    public void Setup(ZoneType zone)
    {
        _zone = zone;
        _neighbors.Clear();
    }

    public void AddNeighbor(Node node)
    {
        if (node != null && node != this && !_neighbors.Contains(node)) _neighbors.Add(node);
    }
    #endregion
}
