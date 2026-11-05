using System;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class TriggerSensor : MonoBehaviour
{
    #region Events
    public event Action<Collider> OnEnter;
    public event Action<Collider> OnExit;
    #endregion

    #region Private Fields
    private SphereCollider _collider;
    #endregion

    #region Properties
    private SphereCollider Collider => _collider != null ? _collider : _collider = GetComponent<SphereCollider>();
    #endregion

    #region Unity Callbacks
    private void Awake() => Collider.isTrigger = true;

    private void OnTriggerEnter(Collider other) => OnEnter?.Invoke(other);

    private void OnTriggerExit(Collider other) => OnExit?.Invoke(other);
    #endregion

    #region Public Methods
    public void SetRadius(float radius) => Collider.radius = radius;
    #endregion
}
