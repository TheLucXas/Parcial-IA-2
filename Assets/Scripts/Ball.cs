using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Ball : MonoBehaviour
{
    #region Events
    public event Action<Ball> OnDestroyed;
    #endregion

    #region Serialized Fields
    [SerializeField] private float _life = 1f;
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _gizmosColor = Color.pink;
    [SerializeField] private float _invincibilityTime = 0.25f;

    [SerializeField] private Image _ballImage;
    [SerializeField] private Sprite[] _ballSprites;
    #endregion

    #region Private Fields
    private static int _activeCount;
    private bool _isInvincible = false;
    private float _maxLife;
    #endregion

    #region Properties
    public static int ActiveCount => _activeCount;
    #endregion

    #region Unity Callbacks
    private void Awake() => _activeCount++;

    private void Start()
    {
        _maxLife = _life;
        int spriteIndex = UnityEngine.Random.Range(0, _ballSprites.Length);
        if (_ballSprites != null && _ballImage != null) _ballImage.sprite = _ballSprites[spriteIndex];
    }

    private void OnDestroy()
    {
        _activeCount--;
        OnDestroyed?.Invoke(this);
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        Gizmos.color = _gizmosColor;
        var pos = new Vector3(transform.position.x, 0f, transform.position.z);
        GizmosUtils.DrawGizmosCircle(pos, Vector3.up, 1f);
    }
    #endregion

    #region Public Methods
    public void TakeDamage(float amount)
    {
        if (_isInvincible) return;

        if (_life <= 0)
        {
            Destroy(gameObject);
            return;
        }

        _life -= amount;

        if (_ballImage != null)
        {
            _ballImage.fillAmount = _life / _maxLife;
        }

        StartCoroutine(InvincibilityTime());
    }
    #endregion

    #region Coroutines
    IEnumerator InvincibilityTime()
    {
        _isInvincible = true;
        yield return new WaitForSeconds(_invincibilityTime);
        _isInvincible = false;
    }
    #endregion
}