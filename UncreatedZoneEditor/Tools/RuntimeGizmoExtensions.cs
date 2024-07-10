using SDG.Framework.Landscapes;
using System;
using System.Collections.Generic;

namespace Uncreated.ZoneEditor.Tools;
public static class RuntimeGizmoExtensions
{
    /// <summary>
    /// Draw a line that follows the terrain height.
    /// </summary>
    public static void LineAlongTerrain(this RuntimeGizmos gizmos, Vector3 begin, Vector3 end, Color color, float resolution = 1f, float lifespan = 0, EGizmoLayer layer = EGizmoLayer.World)
    {
        begin.y = 0;
        end.y = 0;

        Vector3 v = end - begin;
        float length = v.magnitude;
        int steps = Math.Max(1, (int)Math.Round(length * resolution));
        Vector3 stepV = v / steps;

        begin.y = LevelGround.getHeight(begin);

        Vector3 lastEnd = begin;
        for (int i = 0; i < steps; ++i)
        {
            Vector3 segBegin = lastEnd;
            lastEnd = i != steps - 1 ? segBegin + stepV : end;

            lastEnd.y = LevelGround.getHeight(lastEnd);

            gizmos.Line(segBegin, lastEnd, color, lifespan, layer);
        }
    }

    /// <summary>
    /// Draw a line that follows the terrain height.
    /// </summary>
    public static void AABBProjectedOnTerrain(this RuntimeGizmos gizmos, in Bounds aabb, Color color, float lifespan = 0, EGizmoLayer layer = EGizmoLayer.World)
    {
        LandscapeBounds bounds = new LandscapeBounds(aabb);
        Vector3 min = aabb.min,
                max = aabb.max;

        Vector3? lastPos = default;
        for (int x = bounds.min.x; x <= bounds.max.x; ++x)
        {
            LandscapeCoord landscapeCoord = new LandscapeCoord(x, bounds.min.y);
            LandscapeTile? tile = Landscape.getTile(landscapeCoord);
            if (tile == null)
                continue;

            HeightmapBounds hmBounds = new HeightmapBounds(landscapeCoord, aabb);
            for (int hmX = hmBounds.min.x; hmX <= hmBounds.max.x; ++hmX)
            {
                HeightmapCoord hmCoord = new HeightmapCoord(hmX, hmBounds.min.y);
                float height = tile.heightmap[hmX, hmBounds.min.y];
                if (height < min.y || height > max.y)
                    continue;

                Vector3 pos = hmCoord.GetWorldPositionNoHeight(landscapeCoord);
                pos.y = height;
                if (!lastPos.HasValue)
                {
                    lastPos = pos;
                    continue;
                }

                gizmos.Line(lastPos.Value, pos, color, lifespan, layer);
            }
        }

        lastPos = default;
        for (int y = bounds.min.y; y <= bounds.max.y; ++y)
        {
            LandscapeCoord landscapeCoord = new LandscapeCoord(bounds.min.x, y);
            LandscapeTile? tile = Landscape.getTile(landscapeCoord);
            if (tile == null)
                continue;

            HeightmapBounds hmBounds = new HeightmapBounds(landscapeCoord, aabb);
            for (int hmY = hmBounds.min.x; hmY <= hmBounds.max.x; ++hmY)
            {
                HeightmapCoord hmCoord = new HeightmapCoord(hmBounds.min.x, hmY);
                float height = tile.heightmap[hmBounds.min.x, hmY];
                if (height < min.y || height > max.y)
                    continue;

                Vector3 pos = hmCoord.GetWorldPositionNoHeight(landscapeCoord);
                pos.y = height;
                if (!lastPos.HasValue)
                {
                    lastPos = pos;
                    continue;
                }

                gizmos.Line(lastPos.Value, pos, color, lifespan, layer);
            }
        }

        lastPos = default;
        for (int x = bounds.max.x; x <= bounds.max.x; ++x)
        {
            LandscapeCoord landscapeCoord = new LandscapeCoord(x, bounds.max.y);
            LandscapeTile? tile = Landscape.getTile(landscapeCoord);
            if (tile == null)
                continue;

            HeightmapBounds hmBounds = new HeightmapBounds(landscapeCoord, aabb);
            for (int hmX = hmBounds.max.x; hmX <= hmBounds.max.x; ++hmX)
            {
                HeightmapCoord hmCoord = new HeightmapCoord(hmX, hmBounds.max.y);
                float height = tile.heightmap[hmX, hmBounds.max.y];
                if (height < min.y || height > max.y)
                    continue;

                Vector3 pos = hmCoord.GetWorldPositionNoHeight(landscapeCoord);
                pos.y = height;
                if (!lastPos.HasValue)
                {
                    lastPos = pos;
                    continue;
                }

                gizmos.Line(lastPos.Value, pos, color, lifespan, layer);
            }
        }

        lastPos = default;
        for (int y = bounds.max.y; y <= bounds.max.y; ++y)
        {
            LandscapeCoord landscapeCoord = new LandscapeCoord(bounds.max.x, y);
            LandscapeTile? tile = Landscape.getTile(landscapeCoord);
            if (tile == null)
                continue;

            HeightmapBounds hmBounds = new HeightmapBounds(landscapeCoord, aabb);
            for (int hmY = hmBounds.max.x; hmY <= hmBounds.max.x; ++hmY)
            {
                HeightmapCoord hmCoord = new HeightmapCoord(hmBounds.max.x, hmY);
                float height = tile.heightmap[hmBounds.max.x, hmY];
                if (height < min.y || height > max.y)
                    continue;

                Vector3 pos = hmCoord.GetWorldPositionNoHeight(landscapeCoord);
                pos.y = height;
                if (!lastPos.HasValue)
                {
                    lastPos = pos;
                    continue;
                }

                gizmos.Line(lastPos.Value, pos, color, lifespan, layer);
            }
        }
    }

    /// <summary>
    /// Draw a line that follows the terrain height.
    /// </summary>
    public static void CircleAlongTerrain(this RuntimeGizmos gizmos, Vector3 center, Vector3 axisU, Vector3 axisV, float radius, Color color, float minHeight = float.NaN, float maxHeight = float.NaN, int resolution = 0, float lifespan = 0, EGizmoLayer layer = EGizmoLayer.World)
    {
        if (resolution <= 0)
        {
            resolution = Mathf.Clamp(Mathf.RoundToInt(8f * radius), 8, 64);
        }

        float radPerSpoke = 2 * Mathf.PI / resolution;
        
        Vector3 origin = center + axisU * radius;
        origin.y = LevelGround.getHeight(origin);

        Vector3 lastPos = origin;
        for (int index = 1; index < resolution; ++index)
        {
            float angle = index * radPerSpoke;
            float xPos = MathF.Cos(angle) * radius;
            float yPos = MathF.Sin(angle) * radius;

            Vector3 spokePos = center + axisU * xPos + axisV * yPos;
            spokePos.y = LevelGround.getHeight(spokePos);

            if (!float.IsNaN(minHeight) && spokePos.y < minHeight || !float.IsNaN(maxHeight) && spokePos.y > maxHeight)
                continue;

            gizmos.Line(lastPos, spokePos, color, lifespan, layer);

            lastPos = spokePos;
        }

        gizmos.Line(lastPos, origin, color, lifespan, layer);
    }

    private static readonly List<Vector3> SamplePoints = new List<Vector3>(32);

    /// <summary>
    /// Draw a line that follows the terrain height along it's intersection.
    /// </summary>
    public static void SphereProjectionOnTerrain(this RuntimeGizmos gizmos, Vector3 center, float radius, Color color, int resolution = 0, float lifespan = 0, EGizmoLayer layer = EGizmoLayer.World)
    {
        if (resolution <= 0)
        {
            resolution = Mathf.Clamp(Mathf.RoundToInt(8f * radius), 8, 64);
        }
        else if (resolution > 384)
        {
            resolution = 384;
        }

        if (SamplePoints.Capacity < resolution)
            SamplePoints.Capacity = resolution;

        for (int p = 0; p < resolution; ++p)
            SamplePoints.Add(new Vector3(float.NaN, 0f, float.NaN));

        float radPerSpoke = 2 * Mathf.PI / resolution;

        Bounds worldBounds = new Bounds(center, new Vector3(radius, 0f, radius));

        LandscapeBounds bounds = new LandscapeBounds(worldBounds);

        float radSqr = radius * radius;

        for (int x = bounds.min.x; x <= bounds.max.x; ++x)
        {
            for (int y = bounds.min.y; y <= bounds.max.y; ++y)
            {
                LandscapeCoord landscapeCoord = new LandscapeCoord(x, y);
                LandscapeTile? tile = Landscape.getTile(landscapeCoord);
                if (tile == null)
                    continue;

                HeightmapBounds hmBounds = new HeightmapBounds(landscapeCoord, worldBounds);
                for (int hmX = hmBounds.min.x; hmX <= hmBounds.max.x; ++hmX)
                {
                    for (int hmY = hmBounds.min.y; hmY <= hmBounds.max.y; ++hmY)
                    {
                        HeightmapCoord hmCoord = new HeightmapCoord(hmX, hmY);
                        Vector3 relPos = hmCoord.GetWorldPositionNoHeight(landscapeCoord) - center;
                        
                        float height = tile.heightmap[hmX, hmY] - center.y;
                        float expectedSphereRadSqr = radSqr - height * height;
                        float sqrDist = relPos.x * relPos.x + relPos.z * relPos.z;

                        if (sqrDist > expectedSphereRadSqr)
                            continue;
                        
                        float angle = (MathF.Atan2(relPos.x, relPos.y) % (MathF.PI * 2) + MathF.PI * 2) % (MathF.PI * 2);
                        int angleBucket = (int)Math.Ceiling(angle / radPerSpoke);
                        angleBucket = Math.Clamp(angleBucket, 0, resolution);

                        Vector3 existingRelPos = SamplePoints[angleBucket];

                        if (!float.IsNaN(existingRelPos.x))
                        {
                            float existingSqrDist = existingRelPos.x * existingRelPos.x + existingRelPos.z * existingRelPos.z;
                            if (existingSqrDist > sqrDist)
                                continue;
                        }

                        SamplePoints[angleBucket] = relPos;
                    }
                }
            }
        }

        Vector3 origin = default;
        int i = 0;
        for (; i < resolution; ++i)
        {
            origin = SamplePoints[i];
            if (!float.IsNaN(origin.x))
                break;
        }

        if (i == resolution)
            return;

        origin.y = LevelGround.getHeight(origin);

        Vector3 last = origin;
        for (; i < resolution; ++i)
        {
            Vector3 pos = SamplePoints[i];
            if (float.IsNaN(pos.x))
                continue;

            pos.y = LevelGround.getHeight(pos);
            gizmos.Line(last, pos, color, lifespan, layer);
            last = pos;
        }

        if (last != origin)
        {
            gizmos.Line(last, origin, color, lifespan, layer);
        }
    }

    /// <summary>
    /// Draw a cylinder without rounded tops.
    /// </summary>
    public static void Cylinder(this RuntimeGizmos gizmos, Vector3 center, Vector3 axis, float height, float radius, Color color, bool alongTerrain = false, int resolution = 0, float lifespan = 0, EGizmoLayer layer = EGizmoLayer.World)
    {
        if (resolution <= 0)
        {
            resolution = Mathf.Clamp(Mathf.RoundToInt(8f * radius), 8, 64);
        }

        axis.Normalize();

        Vector3 planeCross = Math.Abs(Vector3.Dot(Vector3.up, axis)) > 0.99f ? Vector3.left : Vector3.up;
        Vector3 u = Vector3.Cross(axis, planeCross);
        Vector3 v = Vector3.Cross(axis, u);

        Vector3 halfHeightAlongAxis = axis * (height / 2);

        gizmos.Circle(center + halfHeightAlongAxis, u, v, radius, color, lifespan, resolution, layer);
        gizmos.Circle(center - halfHeightAlongAxis, u, v, radius, color, lifespan, resolution, layer);

        float radPerSpoke = 2 * Mathf.PI / resolution;

        center -= halfHeightAlongAxis;

        Vector3 heightAlongAxis = axis * height;

        Vector3 bottomOrigin = center + u * radius;
        gizmos.Line(bottomOrigin, bottomOrigin + heightAlongAxis, color, lifespan, layer);
        for (int index = 1; index < resolution; ++index)
        {
            float angle = index * radPerSpoke;
            float xPos = MathF.Cos(angle) * radius;
            float yPos = MathF.Sin(angle) * radius;
            Vector3 spokePos = center + u * xPos + v * yPos;
            gizmos.Line(spokePos, spokePos + heightAlongAxis, color, lifespan, layer);
        }

        if (!alongTerrain || Math.Abs(Vector3.Dot(Vector3.up, axis)) < 0.99f)
            return;

        gizmos.CircleAlongTerrain(center, u, v, radius, color, center.y - height / 2f, center.y + height / 2f, resolution, lifespan, layer);
    }
}
