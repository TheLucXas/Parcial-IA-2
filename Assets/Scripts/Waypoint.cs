using UnityEngine;

public class Waypoint : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _gizmosColor = Color.cyan;
    #endregion

    #region Unity Callbacks
    private void Start()
    {
    }

    private void Update()
    {
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Gizmos.color = _gizmosColor;
        var pos = new Vector3(transform.position.x, 0f, transform.position.z);
        Gizmos.DrawWireCube(pos, new Vector3(1f, 0f, 1f));
    }
    #endregion
}