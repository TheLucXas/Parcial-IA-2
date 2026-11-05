using System;
using System.Collections.Generic;
using UnityEngine;

public static class Pathfinding
{
    #region Public Methods
    public static List<Node> AStar(Node startNode, Node endNode, Func<Node, bool> isTransitable = null)
    {
        List<Node> path = new();

        if (startNode == null || endNode == null) return path;
        if (isTransitable != null && (!isTransitable(startNode) || !isTransitable(endNode))) return path;

        PriorityQueue<Node> frontier = new();
        frontier.Enqueue(startNode, 0f);

        Dictionary<Node, Node> cameFrom = new() { { startNode, null } };
        Dictionary<Node, float> costSoFar = new() { { startNode, 0f } };
        HashSet<Node> closed = new();

        Vector3 endPosition = endNode.Position;

        while (!frontier.IsEmpty)
        {
            Node current = frontier.Dequeue();

            if (!closed.Add(current)) continue;

            if (current == endNode)
            {
                while (current != null)
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                path.Reverse();
                return path;
            }

            foreach (Node neighbor in current.Neighbors)
            {
                if (neighbor == null || closed.Contains(neighbor)) continue;
                if (isTransitable != null && !isTransitable(neighbor)) continue;

                float newCost = costSoFar[current] + Vector3.Distance(current.Position, neighbor.Position);
                if (costSoFar.TryGetValue(neighbor, out float oldCost) && newCost >= oldCost) continue;

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                frontier.Enqueue(neighbor, newCost + Vector3.Distance(neighbor.Position, endPosition));
            }
        }

        return path;
    }

    public static float GetPathLength(IReadOnlyList<Node> path)
    {
        float length = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector3.Distance(path[i - 1].Position, path[i].Position);
        }
        return length;
    }
    #endregion
}
