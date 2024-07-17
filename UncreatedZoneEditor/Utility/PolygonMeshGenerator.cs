using System;
using System.Collections.Generic;

namespace Uncreated.ZoneEditor.Utility;
public static class PolygonMeshGenerator
{
    public static Mesh CreateMesh(IReadOnlyList<Vector2> pointList, int triCount, float minHeight, float maxHeight, Vector3? originOverride, out Vector3 origin)
    {
        int ptCt = pointList.Count;
        if (ptCt < 3)
            throw new ArgumentException("Polygons must have at least 3 points to create a mesh.", "proximity");

        bool isReversed = IsCounterclockwise(pointList);

        Vector2[] points;
        if (isReversed)
        {
            points = new Vector2[ptCt];
            for (int i = 0; i < ptCt; ++i)
            {
                points[i] = pointList[ptCt - i - 1];
            }
        }
        else if (pointList is Vector2[] ptArr)
        {
            Vector2[] pts = new Vector2[ptArr.Length];
            Array.Copy(ptArr, pts, pts.Length);
            points = pts;
        }
        else
        {
            points = new Vector2[ptCt];
            for (int i = 0; i < ptCt; ++i)
            {
                points[i] = pointList[i];
            }
        }

        if (originOverride.HasValue)
        {
            origin = originOverride.Value;
        }
        else
        {
            origin = default;
            for (int i = 0; i < ptCt; ++i)
            {
                ref Vector2 pt = ref points[i];
                origin.x += pt.x;
                origin.z += pt.y;
            }

            origin /= ptCt;
        }

        int capTriCount = ptCt - 2;
        if (triCount < 0)
            triCount = capTriCount;
        else if (triCount > capTriCount)
            triCount = capTriCount;

        Vector3[] vertices = new Vector3[ptCt * 6];
        int[] tris = new int[ptCt * 6 + capTriCount * 6];
        Vector3[] normals = new Vector3[ptCt * 6];
        //Vector2[] uv = new Vector2[ptCt * 6];

        Vector2 origin2d = new Vector2(origin.x, origin.z);

        for (int i = 0; i < ptCt; ++i)
        {
            int nextIndex = (i + 1) % ptCt;

            Vector2 pt = points[i] - origin2d;
            Vector2 nextPoint = points[nextIndex] - origin2d;

            int vertStartIndex = i * 4;

            vertices[vertStartIndex] = new Vector3(pt.x, minHeight, pt.y);
            vertices[vertStartIndex + 1] = new Vector3(pt.x, maxHeight, pt.y);
            vertices[vertStartIndex + 2] = new Vector3(nextPoint.x, minHeight, nextPoint.y);
            vertices[vertStartIndex + 3] = new Vector3(nextPoint.x, maxHeight, nextPoint.y);

            Vector2 dir = (nextPoint - pt).normalized;
            Vector3 faceNormal = Vector3.Cross(new Vector3(dir.x, 0, dir.y), Vector3.up);

            normals[vertStartIndex] = faceNormal;
            normals[vertStartIndex + 1] = faceNormal;
            normals[vertStartIndex + 2] = faceNormal;
            normals[vertStartIndex + 3] = faceNormal;

            // top
            vertices[ptCt * 4 + i] = new Vector3(pt.x, maxHeight, pt.y);
            normals[ptCt * 4 + i] = Vector3.up;

            // bottom
            vertices[ptCt * 5 + i] = new Vector3(pt.x, minHeight, pt.y);
            normals[ptCt * 5 + i] = Vector3.down;

            int triStartIndex = i * 6;

            tris[triStartIndex] = vertStartIndex + 1;
            tris[triStartIndex + 1] = vertStartIndex;
            tris[triStartIndex + 2] = vertStartIndex + 2;
            tris[triStartIndex + 3] = vertStartIndex + 1;
            tris[triStartIndex + 4] = vertStartIndex + 2;
            tris[triStartIndex + 5] = vertStartIndex + 3;
        }

        Array.Reverse(points);

        int triOffset = ptCt * 6;
        int triCountWritten = new PolygonTriangulationProcessor(points, ptCt * 4)
                                .WriteTriangles(new ArraySegment<int>(tris, triOffset, (ptCt - 2) * 3), triCount);

        // flip triangles for bottom
        for (int i = 0; i < triCountWritten; ++i)
        {
            int toIndex = triOffset + triCountWritten * 3 + i * 3;
            int fromIndex = triOffset + i * 3;

            tris[fromIndex] = ptCt - (tris[fromIndex] - ptCt * 4) + ptCt * 4 - 1;
            tris[fromIndex + 1] = ptCt - (tris[fromIndex + 1] - ptCt * 4) + ptCt * 4 - 1;
            tris[fromIndex + 2] = ptCt - (tris[fromIndex + 2] - ptCt * 4) + ptCt * 4 - 1;

            tris[toIndex] = tris[fromIndex + 2] + ptCt;
            tris[toIndex + 1] = tris[fromIndex + 1] + ptCt;
            tris[toIndex + 2] = tris[fromIndex] + ptCt;
        }

        Mesh mesh = new Mesh
        {
            name = "Polygon[" + ptCt + "]",
            vertices = vertices,
            triangles = new ArraySegment<int>(tris, 0, triOffset + triCountWritten * 6).ToArray(),
            normals = normals
            //uv = uv
        };

        return mesh;
    }

    // http://www.faqs.org/faqs/graphics/algorithms-faq/ Subject 2.07
    private static bool IsCounterclockwise(IReadOnlyList<Vector2> points)
    {
        int ptCt = points.Count;
        if (ptCt < 3)
            throw new ArgumentException("Polygons must have at least 3 points to create a mesh.", "proximity");

        // find most bottom left point, guaranteed to be on the convex hull.
        Vector2 minPt = points[0];
        int minPtIndex = 0;
        for (int i = 1; i < ptCt; ++i)
        {
            Vector2 pt = points[i];
            if (pt.y < minPt.y || pt.y == minPt.y && pt.x < minPt.x)
            {
                minPt = pt;
                minPtIndex = i;
            }
        }

        Vector2 pt1 = points[(minPtIndex == 0 ? ptCt : minPtIndex) - 1],
                pt3 = points[(minPtIndex + 1) % ptCt];

        Vector3 crx = Vector3.Cross(pt1 - minPt, pt3 - minPt);

        return crx.z < 0;
    }
}