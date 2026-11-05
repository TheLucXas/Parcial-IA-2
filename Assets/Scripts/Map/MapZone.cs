using System;
using System.Collections.Generic;
using UnityEngine;

public enum ZoneType { Corridor, Start, Coto, SafeZone, Exit }

[RequireComponent(typeof(BoxCollider))]
public class MapZone : MonoBehaviour
{
    #region Events
    public static event Action<MapZone, HunterAgent> OnHunterEnteredSafeZone;
    #endregion

    #region Serialized Fields
    [SerializeField] private ZoneType _type = ZoneType.Corridor;
    [SerializeField] private string _displayName = "Zone";
    [SerializeField] private Vector2 _size = new(8f, 8f);
    [SerializeField] private Node _centerNode;
    [SerializeField] private bool _drawGizmos = true;
    #endregion

    #region Private Fields
    private static readonly List<MapZone> _allZones = new();
    #endregion

    #region Properties
    public ZoneType Type => _type;
    public string DisplayName => _displayName;
    public Node CenterNode => _centerNode;
    public Vector3 Center => PerceptionUtils.Flat(transform.position);
    #endregion

    #region Unity Callbacks
    private void OnEnable() => _allZones.Add(this);

    private void OnDisable() => _allZones.Remove(this);

    private void OnValidate()
    {
        if (TryGetComponent(out BoxCollider box))
        {
            box.isTrigger = true;
            box.size = new Vector3(_size.x, 4f, _size.y);
            box.center = Vector3.up * 2f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_type != ZoneType.SafeZone || !other.TryGetComponent(out HunterAgent hunter)) return;

        hunter.SetSafeZone(this);
        OnHunterEnteredSafeZone?.Invoke(this, hunter);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_type != ZoneType.SafeZone || !other.TryGetComponent(out HunterAgent hunter)) return;

        hunter.ClearSafeZone(this);
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Color color = GetZoneColor(_type);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Center + Vector3.up * 0.1f, new Vector3(_size.x, 0f, _size.y));
        Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
        Gizmos.DrawCube(Center + Vector3.up * 0.05f, new Vector3(_size.x, 0f, _size.y));
    }
    #endregion

    #region Public Methods
    public bool Contains(Vector3 position)
    {
        Vector3 local = position - transform.position;
        return Mathf.Abs(local.x) <= _size.x * 0.5f && Mathf.Abs(local.z) <= _size.y * 0.5f;
    }

    public static MapZone GetZoneAt(Vector3 position)
    {
        foreach (MapZone zone in _allZones)
        {
            if (zone.Contains(position)) return zone;
        }
        return null;
    }

    public static bool IsInside(ZoneType type, Vector3 position)
    {
        foreach (MapZone zone in _allZones)
        {
            if (zone._type == type && zone.Contains(position)) return true;
        }
        return false;
    }

    public static MapZone GetFirst(ZoneType type)
    {
        foreach (MapZone zone in _allZones)
        {
            if (zone._type == type) return zone;
        }
        return null;
    }

    public static void GetZones(ZoneType type, List<MapZone> result)
    {
        result.Clear();
        foreach (MapZone zone in _allZones)
        {
            if (zone._type == type) result.Add(zone);
        }
    }

    public static Color GetZoneColor(ZoneType type) => type switch
    {
        ZoneType.Start => new Color(0.25f, 0.55f, 1f),
        ZoneType.Coto => new Color(0.3f, 0.85f, 0.3f),
        ZoneType.SafeZone => new Color(0.2f, 1f, 0.9f),
        ZoneType.Exit => new Color(1f, 0.8f, 0.1f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };
    #endregion
}
