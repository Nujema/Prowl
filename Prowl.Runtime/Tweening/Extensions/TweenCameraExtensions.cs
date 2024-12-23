// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Runtime.Tweening;

namespace Prowl.Runtime;

public static class TweenCameraExtensions
{
    public static Tween TweenFieldOfView(this Camera camera, float to, float duration)
    {
        return Tween.TweenFromTo(x => camera.FieldOfView = x, camera.FieldOfView, to, duration);
    }

    public static Tween TweenShake(this Camera camera, float duration, float shakeAmount = 0.1f, float frequency = 20f)
    {
        var initialPosition = camera.Transform.localPosition;
        var tween = TweenManager.GetOrCreateTween(duration);

        tween._onTick = easedTime => camera.Transform.localPosition = Easing.Shake(
            originalPosition: camera.Transform.localPosition,
            t: easedTime,
            shakeAmount: shakeAmount,
            frequency: frequency);

        tween.OnComplete(() => camera.Transform.TweenMove(initialPosition, 0.5f).SetEase(Ease.SineOut));

        return tween;
    }
}
