using System.Collections.Generic;
using UnityEngine;

public class FiniteStateMachine<T>
{
    #region Private Fields
    private State<T> _currentState;

    private readonly Dictionary<T, State<T>> _allStates = new();
    #endregion

    #region Properties
    public T CurrentStateID { get; private set; }
    #endregion

    #region Public Methods
    public void AddState(T id, State<T> state)
    {
        state.SetFSM(this);

        if (!_allStates.ContainsKey(id))
            _allStates.Add(id, state);
        else
            _allStates[id] = state;
    }

    public void ChangeState(T id)
    {
        if (!_allStates.TryGetValue(id, out State<T> nextState))
        {
            Debug.LogError($"Missing State {id}!");
            return;
        }

        _currentState?.Exit();
        _currentState = nextState;
        CurrentStateID = id;
        _currentState.Enter();
    }

    public void Update()
    {
        if (_currentState != null) _currentState.Update();
    }
    #endregion
}
