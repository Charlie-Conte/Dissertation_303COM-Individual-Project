using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimplifyMesh : MonoBehaviour
{
    public MeshFilter meshFilter;
    // Start is called before the first frame update
    public float quality = 0.6f;
    public void Go()
    {

        var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
        meshSimplifier.Initialize(meshFilter.mesh);
        meshSimplifier.SimplifyMesh(quality);
        var destMesh = meshSimplifier.ToMesh();

        meshFilter.mesh = destMesh;
    }
    public void SaveMesh()
    {
        //AssetDatabase.CreateAsset(meshFilter.mesh, "Assets/Models/Output/" + quality.ToString()+".asset");
        //AssetDatabase.SaveAssets();
    }
    public void ResetScene()
    {
        SceneManager.LoadScene(0);
    }

    public void PickLOD(int LOD)
    {
        if (LOD == 0)
        {
            quality = 0.6f;
        }
        else if (LOD == 1)
        {
            quality = 0.3f;
        }
        else quality = 0.1f;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
