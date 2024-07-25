using System;

namespace Uncreated.ZoneEditor.Utility;
internal static class GraphicsHelper
{
    public static float SqrDistanceToLine(in Vector2 rayOrigin, in Vector2 rayDirection, in Vector2 point)
    {
        if (Math.Abs(rayDirection.x) <= float.Epsilon)
        {
            return Math.Abs(point.x - rayOrigin.x);
        }

        float a = rayDirection.y / rayDirection.x;
        float c = rayOrigin.y - a * rayOrigin.x;

        float div = a * point.x - point.y + c;
        float sqrDist = div * div / (a * a + 1);
        return sqrDist;
    }

    public static Vector2 ClosestPointOnLine(in Vector2 rayOrigin, in Vector2 rayDirection, in Vector2 point)
    {
        if (Math.Abs(rayDirection.x) <= float.Epsilon)
        {
            return new Vector2(rayOrigin.x, point.y);
        }

        float a = rayDirection.y / rayDirection.x;
        float c = rayOrigin.y - a * rayOrigin.x;

        return new Vector2(
            (-(-point.x - a * point.y) - a * c) / (a * a + 1),
            (a * (point.x + a * point.y) + c) / (a * a + 1)
        );
    }

}
