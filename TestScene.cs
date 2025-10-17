using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Globalization;

public class TestScene : MonoBehaviour
{
    public Material meshMaterial;
    private GameObject meshObject1;
    private GameObject meshObject2;
    
    void Start()
    {
        // Create camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        
        cam.transform.position = new Vector3(125, 135, -10);
        cam.orthographic = true;
        cam.orthographicSize = 20;
        cam.backgroundColor = Color.white;
        
        // Create material
        if (meshMaterial == null)
        {
            meshMaterial = new Material(Shader.Find("Unlit/Color"));
            meshMaterial.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        }
        
        // Test both datasets
        TestDataset1();
        TestDataset2();
    }
    
    void TestDataset1()
    {
        List<Vector3> points = LoadPointsFromFile("/workspace/project/setOne.txt");
        if (points != null && points.Count > 0)
        {
            meshObject1 = new GameObject("Mesh1");
            FinalSolution meshGen = meshObject1.AddComponent<FinalSolution>();
            
            // Position first mesh
            meshObject1.transform.position = new Vector3(0, 10, 0);
            
            meshGen.CreateFilledMesh(points);
            
            // Apply material
            MeshRenderer mr = meshObject1.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = meshMaterial;
            }
            
            Debug.Log($"Dataset 1: Loaded {points.Count} points");
        }
    }
    
    void TestDataset2()
    {
        List<Vector3> points = LoadPointsFromFile("/workspace/project/setTwo.txt");
        if (points != null && points.Count > 0)
        {
            meshObject2 = new GameObject("Mesh2");
            FinalSolution meshGen = meshObject2.AddComponent<FinalSolution>();
            
            // Position second mesh below first
            meshObject2.transform.position = new Vector3(0, -10, 0);
            
            meshGen.CreateFilledMesh(points);
            
            // Apply different color material
            Material mat2 = new Material(Shader.Find("Unlit/Color"));
            mat2.color = new Color(0.2f, 0.2f, 0.8f, 0.8f);
            
            MeshRenderer mr = meshObject2.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = mat2;
            }
            
            Debug.Log($"Dataset 2: Loaded {points.Count} points");
        }
    }
    
    List<Vector3> LoadPointsFromFile(string filePath)
    {
        List<Vector3> points = new List<Vector3>();
        
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
                return points;
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
            Debug.LogError($"Error loading file {filePath}: {e.Message}");
        }
        
        return points;
    }
    
    void Update()
    {
        // Allow switching between datasets
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (meshObject1 != null) meshObject1.SetActive(!meshObject1.activeSelf);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (meshObject2 != null) meshObject2.SetActive(!meshObject2.activeSelf);
        }
    }
}