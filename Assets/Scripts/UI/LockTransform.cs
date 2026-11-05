using UnityEngine;

public class LockTransform : MonoBehaviour
{
    #region Serialized Fields
    [Header("Position")]
    [SerializeField] private Vector3 _offset = new Vector3(1f, 0f, 1f);

    [Header("Rotation")]
    [SerializeField] private Vector3 _fixedRotation = new Vector3(90f, 0f, 0f);
    #endregion

    #region Private Fields
    private Transform _parent;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        _parent = transform.parent;
    }

    private void LateUpdate()
    {
        if (_parent != null)
        {
            transform.position = _parent.position + _offset;

            transform.rotation = Quaternion.Euler(_fixedRotation);
        }
    }
    #endregion
}