using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    public void CreateFilledMesh(List<Vector3> points)
    {
        if (points == null || points.Count < 3) return;

        // Remove duplicate last point if it matches first (closed loop)
        List<Vector2> polygon = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = new Vector2(points[i].x, points[i].y);
            if (i == points.Count - 1 && Vector2.Distance(p, new Vector2(points[0].x, points[0].y)) < 0.001f)
                break; // Skip duplicate closing point
            polygon.Add(p);
        }

        if (polygon.Count < 3) return;

        // Triangulate using ear clipping with self-intersection handling
        List<Vector2> vertices = new List<Vector2>();
        List<int> triangles = new List<int>();
        
        TriangulatePolygon(polygon, vertices, triangles);

        if (triangles.Count == 0) return;

        // Convert back to Vector3 and create mesh
        List<Vector3> meshVertices = new List<Vector3>();
        foreach (var v in vertices)
        {
            meshVertices.Add(new Vector3(v.x, v.y, 0));
        }

        Mesh mesh = new Mesh();
        mesh.name = "FilledLoopMesh";
        mesh.SetVertices(meshVertices);
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

    private void TriangulatePolygon(List<Vector2> polygon, List<Vector2> outVertices, List<int> outTriangles)
    {
        // For self-intersecting polygons, use scanline triangulation with even-odd rule
        if (HasSelfIntersections(polygon))
        {
            ScanlineTriangulate(polygon, outVertices, outTriangles);
        }
        else
        {
            // Simple polygon - use ear clipping
            EarClipTriangulate(polygon, outVertices, outTriangles);
        }
    }

    private bool HasSelfIntersections(List<Vector2> polygon)
    {
        int n = polygon.Count;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue; // Adjacent edges
                
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % n];
                Vector2 p3 = polygon[j];
                Vector2 p4 = polygon[(j + 1) % n];
                
                if (LineSegmentsIntersect(p1, p2, p3, p4))
                    return true;
            }
        }
        return false;
    }

    private bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = CrossProduct(p4 - p3, p1 - p3);
        float d2 = CrossProduct(p4 - p3, p2 - p3);
        float d3 = CrossProduct(p2 - p1, p3 - p1);
        float d4 = CrossProduct(p2 - p1, p4 - p1);
        
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
            
        return false;
    }

    private float CrossProduct(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void ScanlineTriangulate(List<Vector2> polygon, List<Vector2> outVertices, List<int> outTriangles)
    {
        // For self-intersecting polygons, use a more robust approach
        // Find all intersection points and break polygon into simple parts
        List<Vector2> allPoints = new List<Vector2>(polygon);
        List<List<int>> simplePolygons = new List<List<int>>();
        
        // Find intersection points
        List<Vector2> intersectionPoints = new List<Vector2>();
        for (int i = 0; i < polygon.Count; i++)
        {
            for (int j = i + 2; j < polygon.Count; j++)
            {
                if (i == 0 && j == polygon.Count - 1) continue;
                
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % polygon.Count];
                Vector2 p3 = polygon[j];
                Vector2 p4 = polygon[(j + 1) % polygon.Count];
                
                Vector2 intersection;
                if (GetLineIntersection(p1, p2, p3, p4, out intersection))
                {
                    intersectionPoints.Add(intersection);
                    allPoints.Add(intersection);
                }
            }
        }
        
        // If we have intersections, use a simplified approach:
        // Create a grid-based triangulation
        if (intersectionPoints.Count > 0)
        {
            GridTriangulate(polygon, outVertices, outTriangles);
        }
        else
        {
            // No intersections, use ear clipping
            EarClipTriangulate(polygon, outVertices, outTriangles);
        }
    }
    
    private void GridTriangulate(List<Vector2> polygon, List<Vector2> outVertices, List<int> outTriangles)
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
        
        // Create a grid and test each cell
        int gridSize = 50; // Adjust for quality vs performance
        float stepX = (maxX - minX) / gridSize;
        float stepY = (maxY - minY) / gridSize;
        
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                float x = minX + i * stepX;
                float y = minY + j * stepY;
                
                Vector2 center = new Vector2(x + stepX * 0.5f, y + stepY * 0.5f);
                
                // Use even-odd rule to test if point is inside
                if (IsPointInsidePolygonEvenOdd(center, polygon))
                {
                    // Create two triangles for this grid cell
                    int baseIndex = outVertices.Count;
                    
                    outVertices.Add(new Vector2(x, y));
                    outVertices.Add(new Vector2(x + stepX, y));
                    outVertices.Add(new Vector2(x, y + stepY));
                    outVertices.Add(new Vector2(x + stepX, y + stepY));
                    
                    // First triangle
                    outTriangles.Add(baseIndex);
                    outTriangles.Add(baseIndex + 1);
                    outTriangles.Add(baseIndex + 2);
                    
                    // Second triangle
                    outTriangles.Add(baseIndex + 1);
                    outTriangles.Add(baseIndex + 3);
                    outTriangles.Add(baseIndex + 2);
                }
            }
        }
    }
    
    private bool GetLineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        
        float denom = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
        if (Mathf.Abs(denom) < 0.0001f) return false;
        
        float t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / denom;
        float u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / denom;
        
        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            intersection = p1 + t * (p2 - p1);
            return true;
        }
        
        return false;
    }
    
    private bool IsPointInsidePolygonEvenOdd(Vector2 point, List<Vector2> polygon)
    {
        int intersections = 0;
        
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = polygon[(i + 1) % polygon.Count];
            
            if (((p1.y <= point.y) && (point.y < p2.y)) || ((p2.y <= point.y) && (point.y < p1.y)))
            {
                float x = p1.x + (point.y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y);
                if (point.x < x)
                    intersections++;
            }
        }
        
        return (intersections % 2) == 1;
    }

    private void EarClipTriangulate(List<Vector2> polygon, List<Vector2> outVertices, List<int> outTriangles)
    {
        outVertices.AddRange(polygon);
        
        List<int> indices = new List<int>();
        for (int i = 0; i < polygon.Count; i++)
            indices.Add(i);

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
                    // Add triangle
                    outTriangles.Add(prev);
                    outTriangles.Add(curr);
                    outTriangles.Add(next);
                    
                    // Remove ear vertex
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            
            if (!earFound) break; // Prevent infinite loop
        }
        
        // Add final triangle
        if (indices.Count == 3)
        {
            outTriangles.Add(indices[0]);
            outTriangles.Add(indices[1]);
            outTriangles.Add(indices[2]);
        }
    }

    private bool IsEar(List<Vector2> polygon, int prev, int curr, int next, List<int> indices)
    {
        Vector2 a = polygon[prev];
        Vector2 b = polygon[curr];
        Vector2 c = polygon[next];
        
        // Check if triangle is oriented correctly (counter-clockwise)
        if (CrossProduct(b - a, c - a) <= 0)
            return false;
        
        // Check if any other vertex is inside this triangle
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
        float d1 = CrossProduct(b - a, p - a);
        float d2 = CrossProduct(c - b, p - b);
        float d3 = CrossProduct(a - c, p - c);
        
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        
        return !(hasNeg && hasPos);
    }
}