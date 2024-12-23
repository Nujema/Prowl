// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Tweening;

namespace Prowl.Runtime;

public static class TweenTransformExtensions
{
    public static Tween TweenMove(this Transform transform, Vector3 fromPosition, Vector3 toPosition, float duration)
        => Tween.TweenFromTo(x => transform.localPosition = x, fromPosition, toPosition, duration);

    public static Tween TweenMove(this Transform transform, Vector3 toPosition, float duration)
        => transform.TweenMove(transform.localPosition, toPosition, duration);
}
