using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Globalization;

public class TestMeshGenerator : MonoBehaviour
{
    private MeshGenerator meshGen;
    
    void Start()
    {
        meshGen = gameObject.AddComponent<MeshGenerator>();
        
        // Test with dataset one
        TestDataset("setOne.txt");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestDataset("setOne.txt");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TestDataset("setTwo.txt");
        }
    }
    
    void TestDataset(string filename)
    {
        List<Vector3> points = LoadPointsFromFile(filename);
        if (points != null && points.Count > 0)
        {
            Debug.Log($"Loaded {points.Count} points from {filename}");
            meshGen.CreateFilledMesh(points);
        }
        else
        {
            Debug.LogError($"Failed to load points from {filename}");
        }
    }
    
    List<Vector3> LoadPointsFromFile(string filename)
    {
        List<Vector3> points = new List<Vector3>();
        
        try
        {
            string filePath = Path.Combine(Application.dataPath, "..", filename);
            if (!File.Exists(filePath))
            {
                filePath = filename; // Try relative path
            }
            
            string[] lines = File.ReadAllLines(filePath);
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                // Parse format: (x, y)
                if (trimmed.StartsWith("(") && trimmed.EndsWith(")"))
                {
                    string coords = trimmed.Substring(1, trimmed.Length - 2);
                    string[] parts = coords.Split(',');
                    
                    if (parts.Length >= 2)
                    {
                        if (float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                            float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                        {
                            points.Add(new Vector3(x, y, 0));
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading file {filename}: {e.Message}");
        }
        
        return points;
    }
}