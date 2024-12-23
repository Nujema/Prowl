// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

namespace Prowl.Runtime.Tweening;

public class Tween
{
    public enum AnimationLoopMode { Forward, BackAndForth }
    internal enum TweenState { Running, Paused, RequestingDestruction, Destroyed }


    internal float _loopDuration;
    internal TweenState _tweenState;
    private uint _loopCount;

    private float _elapsedTime;
    private float _delayTime;
    public float? Duration() => _isEndless ? null : _delayTime + _loopDuration * _numLoops;

    private uint _numLoops;
    private bool _isEndless;
    private bool _inverseDirection;
    private Ease _ease;
    private AnimationLoopMode _animationLoopMode;

    internal Action<float>? _onTick;

    private Action? _onUpdate;
    private Action? _onComplete;

    private GameObject? _ownerGameobject; // todo terminate tween when owner destroyed

    // would be better to have an internal constructor instead, but ObjectPool doesn't allow it
    internal Tween(int internalCall) { }
    public Tween() { Debug.LogError("Direct instantiation of Tween is not allowed."); }

    public static Tween TweenFromTo<T>(Action<T> setter, T fromValue, T toValue, float duration) where T : struct
    {
        var lerpFunc = LerpFuncFactory.GetLerpFunc<T>();
        var tween = TweenManager.GetOrCreateTween(duration);
        tween._onTick = easedTime =>
        {
            T currentValue = tween._inverseDirection
                ? lerpFunc(toValue, fromValue, easedTime)
                : lerpFunc(fromValue, toValue, easedTime);
            setter(currentValue);
        };

        return tween;
    }

    internal void Reset(TweenState tweenState)
    {
        _loopDuration = 1f;
        _elapsedTime = 0f;
        _delayTime = 0f;
        _loopCount = 0;

        _ease = Ease.Linear;
        _numLoops = 1;
        _isEndless = false;
        _inverseDirection = false;
        _animationLoopMode = AnimationLoopMode.Forward;
        _tweenState = tweenState;

        _onTick = null;
        _onComplete = null;
        _onUpdate = null;

        _ownerGameobject = null;
    }

    internal void Update()
    {
        if (_delayTime > 0f)
        {
            _delayTime -= Time.deltaTimeF;
            return;
        }

        _elapsedTime += Time.deltaTimeF;

        float time = MathF.Min(_elapsedTime / _loopDuration, 1f);
        float easedTime = Easing.Calculate(_ease, time);

        _onTick.Invoke(easedTime);
        _onUpdate?.Invoke();

        while (_elapsedTime >= _loopDuration)
        {
            _elapsedTime -= _loopDuration;
            _loopCount++;

            if (_animationLoopMode == AnimationLoopMode.BackAndForth) _inverseDirection = !_inverseDirection;
        }

        if (!_isEndless && _loopCount >= _numLoops) Stop();
    }

    public void Pause()
    {
        if (_tweenState != TweenState.Running) return;
        _tweenState = TweenState.Paused;
    }

    public void Resume()
    {
        if (_tweenState != TweenState.Paused) return;
        _tweenState = TweenState.Running;
    }

    public void Stop()
    {
        if (_tweenState != TweenState.Running && _tweenState != TweenState.Paused) return;
        _onComplete?.Invoke();
        _tweenState = TweenState.RequestingDestruction;
    }

    public Tween SetEase(Ease ease) { _ease = ease; return this; }
    public Tween SetLoopType(AnimationLoopMode animationLoopMode) { _animationLoopMode = animationLoopMode; return this; }
    public Tween SetLoops(uint numLoops) { _numLoops = numLoops; return this; }
    public Tween SetEndless() { _isEndless = true; return this; }
    public Tween SetDelay(float delay) { _delayTime = delay; return this; }
    public Tween OnUpdate(Action callback) { _onUpdate = callback; return this; }
    public Tween OnComplete(Action callback) { _onComplete = callback; return this; }

}
