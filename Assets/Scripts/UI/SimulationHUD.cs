using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class SimulationHUD : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private HunterAgent _hunter;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _eventsText;
    [SerializeField] private int _maxEvents = 8;
    #endregion

    #region Private Fields
    private readonly Queue<string> _events = new();
    private readonly StringBuilder _builder = new();
    #endregion

    #region Unity Callbacks
    private void OnEnable()
    {
        WolfAgent.OnHowl += HandleHowl;
        WolfAgent.OnAlertReceived += HandleAlertReceived;
        FleeState.OnSafeZoneChosen += HandleSafeZoneChosen;
        FleeState.OnSafeZoneWaitStarted += HandleSafeZoneWait;
        GatherState.OnGatherFinished += HandleGatherFinished;
        ExitState.OnExitReached += HandleExitReached;
        HunterAgent.OnCaught += HandleCaught;
        AttackState.OnAttackPerformed += HandleAttack;
    }

    private void OnDisable()
    {
        WolfAgent.OnHowl -= HandleHowl;
        WolfAgent.OnAlertReceived -= HandleAlertReceived;
        FleeState.OnSafeZoneChosen -= HandleSafeZoneChosen;
        FleeState.OnSafeZoneWaitStarted -= HandleSafeZoneWait;
        GatherState.OnGatherFinished -= HandleGatherFinished;
        ExitState.OnExitReached -= HandleExitReached;
        HunterAgent.OnCaught -= HandleCaught;
        AttackState.OnAttackPerformed -= HandleAttack;
    }

    private void Update()
    {
        if (_statusText == null || _hunter == null) return;

        _builder.Clear();
        _builder.AppendLine($"<b>Árbitro (Cazador):</b> {_hunter.CurrentState}  <size=80%>({_hunter.StateLabel})</size>");
        _builder.AppendLine($"Recolecciones: <b>{_hunter.Gathers}/{_hunter.RequiredGathers}</b>   TBA: {Mathf.Max(0f, _hunter.AttackCooldownTimer):0.0}s");
        string safeZone = _hunter.ChosenSafeZone != null ? _hunter.ChosenSafeZone.DisplayName : "-";
        string wolves = _hunter.SeesWolf ? $"<color=#FF5050>SI ({_hunter.VisibleWolves.Count})</color>" : "no";
        _builder.AppendLine($"Ve hinchas: {wolves}   Refugio elegido: {safeZone}");
        _builder.AppendLine($"Muertes: {_hunter.Deaths}   Salidas completadas: {_hunter.CompletedExits}   Pelotas: {Ball.ActiveCount}");

        IReadOnlyList<WolfAgent> allWolves = WolfAgent.AllWolves;
        for (int i = 0; i < allWolves.Count; i++)
        {
            WolfAgent wolf = allWolves[i];
            string lkp = wolf.HasLastKnownPosition ? $" UPC ({wolf.LastKnownPosition.x:0},{wolf.LastKnownPosition.z:0})" : "";
            _builder.AppendLine($"{wolf.name}: {wolf.StateLabel}{lkp}");
        }

        _statusText.text = _builder.ToString();
    }
    #endregion

    #region Private Methods
    private void AddEvent(string message)
    {
        _events.Enqueue($"[{Time.timeSinceLevelLoad:000.0}] {message}");
        while (_events.Count > _maxEvents) _events.Dequeue();

        if (_eventsText == null) return;
        _builder.Clear();
        foreach (string item in _events) _builder.AppendLine(item);
        _eventsText.text = _builder.ToString();
    }

    private void HandleHowl(WolfAgent wolf, Vector3 position) => AddEvent($"<color=#FF5050>{wolf.name} ve al árbitro y grita</color> ({position.x:0},{position.z:0})");

    private void HandleAlertReceived(WolfAgent wolf, Vector3 position) => AddEvent($"<color=#FFD840>{wolf.name} escucha el grito</color>");

    private void HandleSafeZoneChosen(MapZone zone, float length) => AddEvent($"<color=#40FFE0>Árbitro huye a {zone.DisplayName}</color> (camino A* {length:0.0} u)");

    private void HandleSafeZoneWait(MapZone zone) => AddEvent($"Árbitro a salvo en {zone.DisplayName}: los hinchas pierden el objetivo");

    private void HandleGatherFinished(bool completed) => AddEvent(completed ? "<color=#80FF80>Recolección completada</color>" : "Recolección cancelada (no suma)");

    private void HandleExitReached(HunterAgent hunter) => AddEvent("<color=#FFD020>Árbitro llega a la Salida y reinicia el ciclo</color>");

    private void HandleCaught(HunterAgent hunter) => AddEvent($"<color=#FF3030>Árbitro atrapado: Dead ({hunter.RespawnTime:0}s)</color>");

    private void HandleAttack(bool isMelee) => AddEvent(isMelee ? "Ataque cuerpo a cuerpo (TBA reiniciado)" : "Ataque a distancia (TBA reiniciado)");
    #endregion
}
