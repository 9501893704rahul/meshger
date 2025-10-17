using System.Collections.Generic;
using UnityEngine;

public class FinalSolution : MonoBehaviour
{
    public void CreateFilledMesh(List<Vector3> points)
    {
        if (points == null || points.Count < 3) return;

        // Convert to 2D and remove duplicate closing point if present
        List<Vector2> polygon = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = new Vector2(points[i].x, points[i].y);
            if (i == points.Count - 1 && polygon.Count > 0 && Vector2.Distance(p, polygon[0]) < 0.001f)
                break; // Skip duplicate closing point
            polygon.Add(p);
        }

        if (polygon.Count < 3) return;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Check for self-intersections
        if (HasSelfIntersections(polygon))
        {
            // Use grid-based triangulation with even-odd rule
            TriangulateWithGrid(polygon, vertices, triangles);
        }
        else
        {
            // Use ear clipping for simple polygons
            TriangulateWithEarClipping(polygon, vertices, triangles);
        }

        if (triangles.Count == 0) return;

        // Create mesh
        Mesh mesh = new Mesh();
        mesh.name = "FilledLoopMesh";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
        if (mr.sharedMaterial == null)
        {
            mr.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
            mr.sharedMaterial.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        }
    }

    private bool HasSelfIntersections(List<Vector2> polygon)
    {
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // Skip adjacent edges
                
                if (LineSegmentsIntersect(polygon[i], polygon[(i + 1) % n], 
                                        polygon[j], polygon[(j + 1) % n]))
                    return true;
            }
        }
        return false;
    }

    private bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p4 - p3, p1 - p3);
        float d2 = Cross(p4 - p3, p2 - p3);
        float d3 = Cross(p2 - p1, p3 - p1);
        float d4 = Cross(p2 - p1, p4 - p1);
        
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void TriangulateWithGrid(List<Vector2> polygon, List<Vector3> vertices, List<int> triangles)
    {
        // Find bounding box
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        
        foreach (var p in polygon)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        // Grid resolution - balance quality vs performance
        int gridRes = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(polygon.Count) * 8), 20, 80);
        float stepX = (maxX - minX) / gridRes;
        float stepY = (maxY - minY) / gridRes;

        for (int i = 0; i < gridRes; i++)
        {
            for (int j = 0; j < gridRes; j++)
            {
                float x = minX + i * stepX;
                float y = minY + j * stepY;
                Vector2 center = new Vector2(x + stepX * 0.5f, y + stepY * 0.5f);

                if (IsInsidePolygon(center, polygon))
                {
                    int baseIdx = vertices.Count;
                    
                    vertices.Add(new Vector3(x, y, 0));
                    vertices.Add(new Vector3(x + stepX, y, 0));
                    vertices.Add(new Vector3(x, y + stepY, 0));
                    vertices.Add(new Vector3(x + stepX, y + stepY, 0));

                    // Two triangles per grid cell
                    triangles.Add(baseIdx);
                    triangles.Add(baseIdx + 1);
                    triangles.Add(baseIdx + 2);
                    
                    triangles.Add(baseIdx + 1);
                    triangles.Add(baseIdx + 3);
                    triangles.Add(baseIdx + 2);
                }
            }
        }
    }

    private bool IsInsidePolygon(Vector2 point, List<Vector2> polygon)
    {
        int crossings = 0;
        int n = polygon.Count;
        
        for (int i = 0; i < n; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % n];
            
            if (((a.y <= point.y) && (point.y < b.y)) || 
                ((b.y <= point.y) && (point.y < a.y)))
            {
                float x = a.x + (point.y - a.y) * (b.x - a.x) / (b.y - a.y);
                if (point.x < x) crossings++;
            }
        }
        
        return (crossings % 2) == 1; // Even-odd rule
    }

    private void TriangulateWithEarClipping(List<Vector2> polygon, List<Vector3> vertices, List<int> triangles)
    {
        // Convert to Vector3
        foreach (var p in polygon)
            vertices.Add(new Vector3(p.x, p.y, 0));

        List<int> indices = new List<int>();
        for (int i = 0; i < polygon.Count; i++)
            indices.Add(i);

        // Ensure counter-clockwise winding
        if (GetPolygonArea(polygon) < 0)
            indices.Reverse();

        while (indices.Count > 3)
        {
            bool earFound = false;
            
            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];
                
                if (IsEar(polygon, prev, curr, next, indices))
                {
                    triangles.Add(prev);
                    triangles.Add(curr);
                    triangles.Add(next);
                    
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            
            if (!earFound) break; // Prevent infinite loop
        }
        
        // Final triangle
        if (indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }
    }

    private float GetPolygonArea(List<Vector2> polygon)
    {
        float area = 0;
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += polygon[i].x * polygon[j].y - polygon[j].x * polygon[i].y;
        }
        return area * 0.5f;
    }

    private bool IsEar(List<Vector2> polygon, int prev, int curr, int next, List<int> indices)
    {
        Vector2 a = polygon[prev];
        Vector2 b = polygon[curr];
        Vector2 c = polygon[next];
        
        // Check if triangle is convex (counter-clockwise)
        if (Cross(b - a, c - a) <= 0)
            return false;
        
        // Check if any vertex is inside this triangle
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if (idx == prev || idx == curr || idx == next)
                continue;
                
            if (PointInTriangle(polygon[idx], a, b, c))
                return false;
        }
        
        return true;
    }

    private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        
        return !(hasNeg && hasPos);
    }
}