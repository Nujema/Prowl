// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

namespace Prowl.Runtime;

public enum Ease
{
    Linear,
    BackIn,
    BackOut,
    BackInOut,
    BounceIn,
    BounceOut,
    BounceInOut,
    CubicIn,
    CubicOut,
    CubicInOut,
    CircIn,
    CircOut,
    CircInOut,
    ElasticIn,
    ElasticOut,
    ElasticInOut,
    ExpoIn,
    ExpoOut,
    ExpoInOut,
    QuadIn,
    QuadOut,
    QuadInOut,
    QuartIn,
    QuartOut,
    QuartInOut,
    QuintIn,
    QuintOut,
    QuintInOut,
    SineIn,
    SineOut,
    SineInOut,
}

public static class Easing
{
    public static float Calculate(Ease ease, float t) => ease switch
    {
        Ease.Linear => Linear(t),
        Ease.SineIn => SineIn(t),
        Ease.SineOut => SineOut(t),
        Ease.SineInOut => SineInOut(t),
        Ease.QuadIn => QuadIn(t),
        Ease.QuadOut => QuadOut(t),
        Ease.QuadInOut => QuadInOut(t),
        Ease.CubicIn => CubicIn(t),
        Ease.CubicOut => CubicOut(t),
        Ease.CubicInOut => CubicInOut(t),
        Ease.QuartIn => QuartIn(t),
        Ease.QuartOut => QuartOut(t),
        Ease.QuartInOut => QuartInOut(t),
        Ease.QuintIn => QuintIn(t),
        Ease.QuintOut => QuintOut(t),
        Ease.QuintInOut => QuintInOut(t),
        Ease.ExpoIn => ExpoIn(t),
        Ease.ExpoOut => ExpoOut(t),
        Ease.ExpoInOut => ExpoInOut(t),
        Ease.CircIn => CircIn(t),
        Ease.CircOut => CircOut(t),
        Ease.CircInOut => CircInOut(t),
        Ease.BackIn => BackIn(t),
        Ease.BackOut => BackOut(t),
        Ease.BackInOut => BackInOut(t),
        Ease.ElasticIn => ElasticIn(t),
        Ease.ElasticOut => ElasticOut(t),
        Ease.ElasticInOut => ElasticInOut(t),
        Ease.BounceIn => BounceIn(t),
        Ease.BounceOut => BounceOut(t),
        Ease.BounceInOut => BounceInOut(t),
        _ => t
    };

    public static float Linear(float t) => t;

    public static float SineIn(float t) => 1 - (float)Math.Cos(t * Math.PI / 2);
    public static float SineOut(float t) => (float)Math.Sin(t * Math.PI / 2);
    public static float SineInOut(float t) => 0.5f * (1 - (float)Math.Cos(Math.PI * t));

    public static float QuadIn(float t) => t * t;
    public static float QuadOut(float t) => t * (2 - t);
    public static float QuadInOut(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

    public static float CubicIn(float t) => t * t * t;
    public static float CubicOut(float t) => --t * t * t + 1;
    public static float CubicInOut(float t) => t < 0.5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;

    public static float QuartIn(float t) => t * t * t * t;
    public static float QuartOut(float t) => 1 - (--t * t * t * t);
    public static float QuartInOut(float t) => t < 0.5f ? 8 * t * t * t * t : 1 - 8 * (--t) * t * t * t;

    public static float QuintIn(float t) => t * t * t * t * t;
    public static float QuintOut(float t) => --t * t * t * t * t + 1;
    public static float QuintInOut(float t) => t < 0.5f ? 16 * t * t * t * t * t : 1 - 16 * (--t) * t * t * t * t;

    public static float ExpoIn(float t) => t == 0 ? 0 : (float)Math.Pow(2, 10 * (t - 1));
    public static float ExpoOut(float t) => t == 1 ? 1 : 1 - (float)Math.Pow(2, -10 * t);
    public static float ExpoInOut(float t) => t == 0 ? 0 : t == 1 ? 1 : t < 0.5f ? 0.5f * (float)Math.Pow(2, 20 * t - 10) : 1 - 0.5f * (float)Math.Pow(2, -20 * t + 10);

    public static float CircIn(float t) => 1 - (float)Math.Sqrt(1 - t * t);
    public static float CircOut(float t) => (float)Math.Sqrt(1 - (--t * t));
    public static float CircInOut(float t) => t < 0.5f ? 0.5f * (1 - (float)Math.Sqrt(1 - 4 * t * t)) : 0.5f * ((float)Math.Sqrt(1 - (t = t * 2 - 2) * t) + 1);

    public static float BackIn(float t) => t * t * t - t * (float)Math.Sin(t * Math.PI);
    public static float BackOut(float t) => --t * t * t + t * (float)Math.Sin(t * Math.PI) + 1;
    public static float BackInOut(float t) => t < 0.5f ? 0.5f * (t * t * t - t * (float)Math.Sin(t * Math.PI)) : 0.5f * ((--t) * t * t + t * (float)Math.Sin(t * Math.PI) + 2);

    public static float ElasticIn(float t) => t == 0 ? 0 : t == 1 ? 1 : -(float)Math.Pow(2, 10 * (t - 1)) * (float)Math.Sin((t - 1.1f) * (2 * Math.PI) / 0.4f);
    public static float ElasticOut(float t) => t == 0 ? 0 : t == 1 ? 1 : (float)Math.Pow(2, -10 * t) * (float)Math.Sin((t - 0.1f) * (2 * Math.PI) / 0.4f) + 1;
    public static float ElasticInOut(float t) => t == 0 ? 0 : t == 1 ? 1 : t < 0.5f ? -0.5f * (float)Math.Pow(2, 20 * t - 10) * (float)Math.Sin((20 * t - 11.125f) * (2 * Math.PI) / 0.4f) : 0.5f * (float)Math.Pow(2, -20 * t + 10) * (float)Math.Sin((20 * t - 11.125f) * (2 * Math.PI) / 0.4f) + 1;

    public static float BounceIn(float t) => 1 - BounceOut(1 - t);
    public static float BounceOut(float t)
    {
        if (t < 1 / 2.75f) return 7.5625f * t * t;
        else if (t < 2 / 2.75f) return 7.5625f * (t -= 1.5f / 2.75f) * t + 0.75f;
        else if (t < 2.5 / 2.75f) return 7.5625f * (t -= 2.25f / 2.75f) * t + 0.9375f;
        else return 7.5625f * (t -= 2.625f / 2.75f) * t + 0.984375f;
    }
    public static float BounceInOut(float t) => t < 0.5f ? 0.5f * BounceIn(t * 2) : 0.5f * BounceOut(t * 2 - 1) + 0.5f;


    public static Vector3 Shake(Vector3 originalPosition, float t, float shakeAmount = 0.1f, float frequency = 20f)
    {
        return new Vector3(
            originalPosition.x + MathF.Sin(t * frequency) * shakeAmount * (1f - t),
            originalPosition.y + MathF.Cos(t * frequency) * shakeAmount * (1f - t),
            originalPosition.z + MathF.Sin(t * frequency + MathF.PI / 2) * shakeAmount * (1f - t));
    }
}
