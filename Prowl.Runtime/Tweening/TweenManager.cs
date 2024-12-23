// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Prowl.Runtime.Utils;

namespace Prowl.Runtime.Tweening;

internal static class TweenManager
{
    private static readonly List<Tween> _activeTweens = [];
    private static readonly ObjectPool<Tween> _tweenObjectPool = new();

    private static Stopwatch st = new Stopwatch();

    public static void Initialize()
    {
        // Test(100000, 0.5f);
    }

    private static void Test(int numTweens, float duration)
    {
        var foo = EventTimeline.Create()
            .AppendAction(() =>
            {
                st.Restart();
                for (int i = 0; i < numTweens; i++)
                {
                    var v = Vector3.zero;
                    Tween.TweenFromTo(x => v = x, v, Vector3.up, duration);
                }
                Debug.Log($"Init: {st.ElapsedMilliseconds} ms");
            })
            .AppendWait(duration + 1.2f)
            .AppendAction(() =>
            {
                st.Restart();
                for (int i = 0; i < numTweens; i++)
                {
                    var v = Vector3.zero;
                    Tween.TweenFromTo(x => v = x, v, Vector3.up, duration);
                }
                Debug.Log($"Init: {st.ElapsedMilliseconds} ms");
            })
            .AppendWait(duration + 1.2f)
            .AppendAction(() =>
            {
                st.Restart();
                for (int i = 0; i < numTweens; i++)
                {
                    var v = Vector3.zero;
                    Tween.TweenFromTo(x => v = x, v, Vector3.up, duration);
                }
                Debug.Log($"Init: {st.ElapsedMilliseconds} ms");
            });

        Debug.Log(foo);
    }

    public static void Update()
    {
        if (_activeTweens.Count == 0) return;
        st.Restart();
        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            var tween = _activeTweens[i];

            if (tween._tweenState == Tween.TweenState.Running) tween.Update();

            if (tween._tweenState == Tween.TweenState.RequestingDestruction)
            {
                tween.Reset(Tween.TweenState.Destroyed);
                _tweenObjectPool.Release(tween);
                _activeTweens.RemoveAt(i);
                // Debug.Log(_tweenObjectPool);
                // Debug.Log(_activeTweens.Count);
            }
        }

        // Debug.Log($"Update: {st.ElapsedMilliseconds} ms");
    }

    public static Tween GetOrCreateTween(float duration)
    {
        var tween = _tweenObjectPool.Get(() => new Tween(42));
        tween.Reset(Tween.TweenState.Running);
        tween._loopDuration = duration;
        _activeTweens.Add(tween);
        return tween;
    }

}

internal static class LerpFuncFactory
{
    public static Func<T, T, float, T> GetLerpFunc<T>() where T : notnull => typeof(T) switch
    {
        { } type when type == typeof(float) =>
            (a, b, t) => (T)(object)Mathf.LerpUnclamped((float)(object)a, (float)(object)b, t),

        { } type when type == typeof(Color) =>
            (a, b, t) => (T)(object)Color.LerpUnclamped((Color)(object)a, (Color)(object)b, t),

        { } type when type == typeof(Vector2) =>
            (a, b, t) => (T)(object)Vector2.LerpUnclamped((Vector2)(object)a, (Vector2)(object)b, t),

        { } type when type == typeof(Vector3) =>
            (a, b, t) => (T)(object)Vector3.LerpUnclamped((Vector3)(object)a, (Vector3)(object)b, t),

        { } type when type == typeof(Vector4) =>
            (a, b, t) => (T)(object)Vector4.LerpUnclamped((Vector4)(object)a, (Vector4)(object)b, t),

        { } type when type == typeof(Quaternion) =>
            (a, b, t) => (T)(object)Quaternion.Lerp((Quaternion)(object)a, (Quaternion)(object)b, t),

        _ => throw new InvalidOperationException($"Unsupported type for Lerp: {typeof(T).Name}")
    };
}
