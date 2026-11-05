using UnityEngine;

public class Projectile : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private MeshRenderer _renderer;
    #endregion

    #region Private Fields
    private Agent _target;
    private float _damage;

    private float _speed = 40f;
    private float _maxForce = 20f;
    private Vector3 _velocity;

    private Vector3 _startPosition;
    private float _meleeRange;
    private bool _isMeleeShot;

    private float _lifetime = 2f;
    #endregion

    #region Unity Callbacks
    private void Update()
    {
        _lifetime -= Time.deltaTime;

        if (_lifetime <= 0f || _target == null)
        {
            Destroy(gameObject);
            return;
        }

        _velocity += SteeringUtils.CalculatePursuit(transform.position, _velocity, _speed, _maxForce, _target.transform.position, _target.Velocity);
        _velocity.y = 0f;

        transform.position += _velocity * Time.deltaTime;

        if (_velocity.sqrMagnitude > 0.0001f)
        {
            transform.forward = _velocity.normalized;
        }

        if (!_isMeleeShot && Bounds.Instance.IsOutOfBounds(transform.position))
        {
            Destroy(gameObject);
            return;
        }

        if (_isMeleeShot && Vector3.Distance(_startPosition, transform.position) > _meleeRange)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 projPos2D = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 targetPos2D = new Vector3(_target.transform.position.x, 0f, _target.transform.position.z);

        float impactThreshold = 1.5f;

        if (Vector3.Distance(projPos2D, targetPos2D) <= impactThreshold)
        {
            _target.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
    #endregion

    #region Public Methods
    public void Initialize(Agent target, float damage, bool isMelee, float meleeRange)
    {
        _target = target;
        _damage = damage;
        _startPosition = transform.position;
        _isMeleeShot = isMelee;
        _meleeRange = meleeRange;

        if (_renderer != null)
        {
            _renderer.material.color = isMelee ? Color.red : Color.yellow;
            _speed = isMelee ? 60f : 40f;
        }
    }
    #endregion
}