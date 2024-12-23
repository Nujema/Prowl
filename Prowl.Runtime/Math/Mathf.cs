// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Runtime.CompilerServices;

namespace Prowl.Runtime;

public static class Mathf
{
    [MethodImpl(MathD.IN)] public static bool ApproximatelyEquals(float value1, float value2) => Math.Abs(value1 - value2) < float.Epsilon;
    [MethodImpl(MathD.IN)] public static bool ApproximatelyEquals(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => ApproximatelyEquals(a.X, b.X) && ApproximatelyEquals(a.Y, b.Y);
    [MethodImpl(MathD.IN)] public static bool ApproximatelyEquals(System.Numerics.Vector3 a, System.Numerics.Vector3 b) => ApproximatelyEquals(a.X, b.X) && ApproximatelyEquals(a.Y, b.Y) && ApproximatelyEquals(a.Z, b.Z);
    [MethodImpl(MathD.IN)] public static bool ApproximatelyEquals(System.Numerics.Vector4 a, System.Numerics.Vector4 b) => ApproximatelyEquals(a.X, b.X) && ApproximatelyEquals(a.Y, b.Y) && ApproximatelyEquals(a.Z, b.Z) && ApproximatelyEquals(a.W, b.W);

    [MethodImpl(MathD.IN)] public static float LerpUnclamped(float a, float b, float t) => (1f - t) * a + t * b;
    [MethodImpl(MathD.IN)] public static float Lerp(float a, float b, float t) => LerpUnclamped(a, b, Math.Clamp(t, 0f, 1f));
}
