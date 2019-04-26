using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(Decimation_ProposedSimple))]
public class DecimationManager : MonoBehaviour
{
    public static List<Vector3> vert;       // global list of vertices
    public static List<TriangleIndeceIDs> tri;       // global list of triangles
    public static int[] collapse_map;  // to which neighbor each vertex collapses
    Decimation_ProposedSimple proposedSimple;
    public static int[] permutation;
    public static int render_num = 2000;
    public GameObject point;
    public List<GameObject> pointsToView;

    float lodbase = 0.5f; // the fraction of vertices used to morph toward
    float morph = 1.0f;   // where to render between 2 levels of detail


    private void Start()
    {
        proposedSimple = gameObject.GetComponent<Decimation_ProposedSimple>();
    }

    int Map(int a, int mx)
    {
        if (mx <= 0) return 0;
        while (a >= mx)
        {
            a = collapse_map[a];
        }
        return a;
    }

    void DrawModelTriangles()
    {
        Debug.Assert(collapse_map.Length != 0);
        List<CombineInstance> newMeshes = new List<CombineInstance>();
        Mesh finalMesh = new Mesh();
        finalMesh.name = "Decimated Mesh";
        int i = 0;
        for (i = 0; i < tri.Count; i++)
        {
            int p0 = Map(tri[i].v[0], render_num);
            int p1 = Map(tri[i].v[1], render_num);
            int p2 = Map(tri[i].v[2], render_num);
            // note:  serious optimization opportunity here,
            //  by sorting the triangles the following "continue" 
            //  could have been made into a "break" statement.
            if (p0 == p1 || p1 == p2 || p2 == p0) continue;
           

            // if we are not currenly morphing between 2 levels of detail
            // (i.e. if morph=1.0) then q0,q1, and q2 are not necessary.

            int q0 = Map(p0, (int)(render_num * lodbase));
            int q1 = Map(p1, (int)(render_num * lodbase));
            int q2 = Map(p2, (int)(render_num * lodbase));
            Vector3 v0, v1, v2;
            v0 = vert[p0] * morph + vert[q0] * (1 - morph);
            v1 = vert[p1] * morph + vert[q1] * (1 - morph);
            v2 = vert[p2] * morph + vert[q2] * (1 - morph);




            // the purpose of the demo is to show polygons
            // therefore just use 1 face normal (flat shading)
            Vector3 nrml = Vector3.Cross((v1 - v0),(v2 - v1));  // cross product
            if (0 < Vector3.Magnitude(nrml))
            {
                nrml = Vector3.Normalize(nrml);
            }


            Mesh newMesh = new Mesh();


            List<Vector3> nVerts = new List<Vector3>();
            List<Vector3> nNorms = new List<Vector3>();
            List<int> nTri = new List<int>();

            nVerts.Add(v0);
            nVerts.Add(v1);
            nVerts.Add(v2);
            nNorms.Add(nrml);
            nNorms.Add(nrml);
            nNorms.Add(nrml);
            nTri.Add(0);
            nTri.Add(1);
            nTri.Add(2);



            newMesh.vertices = nVerts.ToArray();
            newMesh.triangles = nTri.ToArray();
            newMesh.normals = nNorms.ToArray();
            newMesh.name = i.ToString();
            CombineInstance instance = new CombineInstance();
            instance.mesh = newMesh;
            newMeshes.Add(instance);
            //newMeshes[i].mesh = newMesh;

        }

        finalMesh.CombineMeshes(newMeshes.ToArray(), true, false);
        proposedSimple.meshFilter.mesh = finalMesh;
        //int j = 0;
        //foreach (var item in finalMesh.vertices)
        //{
        //    GameObject v = Instantiate(point, item, Quaternion.identity, gameObject.transform);
        //    v.name = j.ToString();
        //    pointsToView.Add(v);
        //    j++;
        //} 


        AssetDatabase.CreateAsset(finalMesh, "Assets/newMesh.asset");
        AssetDatabase.SaveAssets();
    }

    void PermuteVertices(int[] permutation)
    {
        // rearrange the vertex list 
        List<Vector3> temp_list = new List<Vector3>();
        int i;
        Debug.Assert(permutation.Length == vert.Count);
        for (i = 0; i < vert.Count; i++)
        {
            temp_list.Add(vert[i]);
        }
        for (i = 0; i < vert.Count; i++)
        {
            vert[permutation[i]] = temp_list[i];
        }
        // update the changes in the entries in the triangle list
        for (i = 0; i < tri.Count; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                tri[i].v[j] = permutation[tri[i].v[j]];
            }
        }


    }


    public void Decimate()
    {
        proposedSimple.Prepare();
        permutation = new int[Decimation_ProposedSimple.dVList.Count];
        collapse_map = new int[Decimation_ProposedSimple.dVList.Count];

        proposedSimple.Process(vert, tri);
        PermuteVertices(permutation);
        DrawModelTriangles();
    }
}
public class TriangleIndeceIDs
{
    public int[] v = new int[3];  // indices to vertex list

    public TriangleIndeceIDs(int[] v)
    {
        this.v = v;
    }
};