using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;
using UnityEditor;

public class Decimation_ProposedSimple : MonoBehaviour
{
    public MeshFilter meshFilter;
    Mesh mesh;
    private Mesh newMesh;
    public GameObject point;
    public List<GameObject> pointsToView;
    Vector3[] vertexList;
    int[] indeceList;
    public static Dictionary<int, Vertex> pSVertices;
    public static Dictionary<int, Triangle> pSTriangles;
    //can have gaps e.g.[ 1, 42, 2 ,0]

    // Start is called before the first frame update
    void Start()
    {
        pSVertices = new Dictionary<int, Vertex>();
        pSTriangles = new Dictionary<int,Triangle>();
        newMesh = new Mesh();
        mesh = meshFilter.mesh;
        vertexList = mesh.vertices;
        indeceList = mesh.triangles;

        int idSetter = 0;



        for(int tri = 0; tri < mesh.triangles.Length; tri++)
        {
            //Debug.Log(mesh.vertices[mesh.triangles[tri]][0] + "\t "+mesh.vertices[mesh.triangles[tri]][1] + "\t "+mesh.vertices[mesh.triangles[tri]][2]);

            Triangle currentT = new Triangle(tri);
            Vertex[] currentV= new Vertex[3];

            for (int i = 0; i < 3;i++)
            {
                bool hasVert = false;
                foreach (KeyValuePair<int,Vertex> pSVert in pSVertices)
                {
                    if(pSVert.Value.position.Equals(mesh.vertices[mesh.triangles[tri]][i])) hasVert = true;
                }
                if (!hasVert)
                {
                    currentV[i] = new Vertex(mesh.vertices[mesh.triangles[tri]], mesh.normals[mesh.triangles[tri]], idSetter);

                    if (!currentV[i].face.Contains(currentT))
                    {
                        currentV[i].face.Add(currentT);
                    }
                    
                    idSetter++;
                }

            }
            pSTriangles[tri].SetVertices(currentV[0], currentV[1], currentV[2]);
            
            

        }
        







        //for (int vNumber = 0; vNumber < mesh.vertices.Length; vNumber++)
        //{
        //    new Vertex(mesh.vertices[vNumber], mesh.normals[vNumber], vNumber);
            
        ////    //GameObject v = Instantiate(point, mesh.vertices[vNumber], Quaternion.identity, gameObject.transform);
        ////    //v.name = mesh.vertices[vNumber].ToString();
        ////    //pointsToView.Add(v);
        //}
        //for (int fNumber = 2; fNumber < mesh.triangles.Length; fNumber++)
        //{

        //    mesh.SetTriangles(,)

        //    Debug.Log(mesh.triangles.Length);
        //    new Triangle(pSVertices[(fNumber - 2) * 3], pSVertices[(fNumber - 1) * 3 + 1], pSVertices[fNumber * 3 + 2]);

        //}


    }

    public void Decimate()
    {
        List<Vector3> nVerts = new List<Vector3>();
        //foreach (var item in pSVertices)
        //{
        //    nVerts.Add(item.position + new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));
        //}

        ComputeAllEdgeCollapseCosts();
        while (pSVertices.Count > 0/*desired*/)
        {
            Vertex mn = MinimumCostEdge();
            Collapse(mn, mn.collapse);
        }


        newMesh.vertices = nVerts.ToArray();
        newMesh.triangles = mesh.triangles;
        newMesh.uv = mesh.uv;
        meshFilter.mesh = newMesh;


    }
    Vertex MinimumCostEdge()
    {
        // Find the edge that when collapsed will affect model the least.
        // This funtion actually returns a Vertex, the second vertex
        // of the edge (collapse candidate) is stored in the vertex data.
        // Serious optimization opportunity here: this function currently
        // does a sequential search through an unsorted list :-(
        // Our algorithm could be O(n*lg(n)) instead of O(n*n)
        Vertex mn = pSVertices[0];
        for (int i = 0; i < pSVertices.Count; i++)
        {
            if (pSVertices[i].objDist < mn.objDist)
            {
                mn = pSVertices[i];
            }
        }
        return mn;
    }
    float ComputeEdgeCollapseCost(Vertex u, Vertex v)
    {
        // if we collapse edge uv by moving u to v then how
        // much different will the model change, i.e. the “error”.

        float edgelength = Vector3.Distance(v.position , u.position);
        float curvature=0;
        // find the “sides” triangles that are on the edge uv
        List<Triangle> sides = new List<Triangle>();
        for (int i = 0; i < u.face.Count; i++) 
        {
            if (u.face[i].HasVertex(v))
            {
                sides.Add(u.face[i]);
            }
        }
        // use the triangle facing most away from the sides
        // to determine our curvature term
        for (int i=0;i<u.face.Count;i++)
        {
            float mincurv=1;
            for (int j=0;j < sides.Count;j++)
            {
                // use dot product of face normals.
                float dotprod = Vector3.Dot(u.face[i].normal, sides[j].normal);
                mincurv = Math.Min(mincurv, (1 - dotprod) / 2.0f);
            }
            curvature = Math.Max(curvature, mincurv);
        }
        return edgelength * curvature;
    }

    void ComputeEdgeCostAtVertex(Vertex v)
    {
        // compute the edge collapse cost for all edges that start
        // from vertex v.  Since we are only interested in reducing
        // the object by selecting the min cost edge at each step, we
        // only cache the cost of the least cost edge at this vertex
        // (in member variable collapse) as well as the value of the 
        // cost (in member variable objdist).
        if (v.neighbor.Count==0)
        {
            v.collapse = null;
            v.objDist=-0.01f;
            return;
        }
        v.objDist = 1000000;
        v.collapse = null;// search all neighboring edges for “least cost” edge
        for (int i=0;i < v.neighbor.Count;i++)
        {
            float dist;
            dist = ComputeEdgeCollapseCost(v,v.neighbor[i]);
            if (dist < v.objDist)
            {
                v.collapse = v.neighbor[i];
                v.objDist=dist;
            }
        }
    }

    void ComputeAllEdgeCollapseCosts()
    {
        // For all the edges, compute the difference it would make
        // to the model if it was collapsed.  The least of these
        // per vertex is cached in each vertex object.
        for (int i = 0; i < pSVertices.Count; i++)
        {
            ComputeEdgeCostAtVertex(pSVertices[i]);
        }
    }

    void Collapse(Vertex u, Vertex v)
    {
        // Collapse the edge uv by moving vertex u onto v
        // Actually remove tris on uv, then update tris that
        // have u to have v, and then remove u.
        
        if (v != null)
        {
            
            // u is a vertex all by itself so just delete it
            
            pSVertices.Remove(u.id);
            return;
        }
        int i;
        List<Vertex >tmp = new List<Vertex>();
        // make tmp a list of all the neighbors of u
        for (i=0;i<u.neighbor.Count;i++)
        {
            tmp.Add(u.neighbor[i]);
        }
        // delete triangles on edge uv:
        for (i=u.face.Count-1;i>=0;i--)
        {
            if (u.face[i].HasVertex(v))
            {
                pSTriangles.Remove(u.face[i].id);
            }
        }
        // update remaining triangles to have v instead of u
        for (i = u.face.Count - 1; i >= 0; i--) 
        {
            u.face[i].ReplaceVertex(u,v);
        }
        pSVertices.Remove(u.id);
        // recompute the edge collapse costs in neighborhood
        for (i=0;i<tmp.Count;i++)
        {
            ComputeEdgeCostAtVertex(tmp[i]);
        }
    }







    public class Triangle
    {
        
        public Vertex[] vertices = new Vertex[3]; //triangles 3 points
        public Vector3 normal;
        public int id;
        public Triangle(Vertex v0, Vertex v1, Vertex v2, int _id)
        {
            Debug.Assert(v0 != v1 && v1 != v2 && v2 != v0);  //#mod1
            id = _id;

            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;

            ComputeNormal();
            pSTriangles.Add(id,this);
            for (int i = 0; i < 3; i++)
            {
                vertices[i].face.Add(this);
                for (int j = 0; j < 3; j++) if (i != j)
                    {
                        if(!vertices[i].neighbor.Contains(vertices[j]))
                        {
                            vertices[i].neighbor.Add(vertices[j]);
                        }
                       
                    }
            }
        }

        public Triangle(int _id)
        {
            id = _id;
            pSTriangles.Add(id,this);
        }

        ~Triangle()
        {
            int i;
            pSTriangles.Remove(id);
            for (i = 0; i < 3; i++)
            {
                if (vertices[i] != null) vertices[i].face.Remove(this);
            }
            for (i = 0; i < 3; i++)
            {
                int i2 = (i + 1) % 3;
                if (vertices[i] == null || vertices[i2] == null) continue;
                vertices[i].RemoveIfNonNeighbor(vertices[i2]);
                vertices[i2].RemoveIfNonNeighbor(vertices[i]);
            }
        }

        public void SetVertices(Vertex v0, Vertex v1, Vertex v2)
        {
            Debug.Assert(v0 != v1 && v1 != v2 && v2 != v0);  //#mod1
            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;

            ComputeNormal();

            for (int i = 0; i < 3; i++)
            {
                vertices[i].face.Add(this);
                for (int j = 0; j < 3; j++) if (i != j)
                    {
                        if (!vertices[i].neighbor.Contains(vertices[j]))
                        {
                            vertices[i].neighbor.Add(vertices[j]);
                        }

                    }
            }
        }

        public void ComputeNormal()
        {
            //normal = (vertices[0].normal + vertices[1].normal + vertices[2].normal).normalized;

            Vector3 v0 = vertices[0].position;
            Vector3 v1 = vertices[1].position;
            Vector3 v2 = vertices[2].position;
            normal =  Vector3.Cross((v1 - v0) , (v2 - v1));
            if (Vector3.Magnitude(normal) == 0) return;
            normal = Vector3.Normalize(normal);
        }

        public void ReplaceVertex(Vertex vold, Vertex vnew)
        {
            //vertices[Array.FindIndex(vertices, v => v.position == vold.position)] = vnew;

            Debug.Assert(vold != null && vnew != null);
            Debug.Assert(vold == vertices[0] || vold == vertices[1] || vold == vertices[2]);
            Debug.Assert(vnew != vertices[0] && vnew != vertices[1] && vnew != vertices[2]);
            if (vold == vertices[0])
            {
                vertices[0] = vnew;
            }
            else if (vold == vertices[1])
            {
                vertices[1] = vnew;
            }
            else
            {
                Debug.Assert(vold == vertices[2]);
                vertices[2] = vnew;
            }
            int i;
            vold.face.Remove(this);
            Debug.Assert(!vnew.face.Contains(this));
            vnew.face.Add(this);
            for (i = 0; i < 3; i++)
            {
                vold.RemoveIfNonNeighbor(vertices[i]);
                vertices[i].RemoveIfNonNeighbor(vold);
            }
            for (i = 0; i < 3; i++)
            {
                Debug.Assert(vertices[i].face.Contains(this) == true);
                for (int j = 0; j < 3; j++) if (i != j)
                    {
                        if (!vertices[i].neighbor.Contains(vertices[j]))
                        {
                            vertices[i].neighbor.Add(vertices[j]);
                        }
                    }
            }
            ComputeNormal();
        }

        public bool HasVertex(Vertex v)
        {

            return (v == vertices[0] || v == vertices[1] || v == vertices[2]);
        }

    }

    public class Vertex
    {

        public Vector3 position; // location of this point
        public int id; // place of vertex in original list
       // public Vector3 normal;
        public List<Vertex> neighbor; // adjacent vertices
        public List<Triangle> face; // adjacent triangles
        public float objDist; // cached cost of collapsing edge
        public Vertex collapse; // candidate vertex for collapse

        public Vertex(Vector3 v, Vector3 n, int _id)
        {
            position = v;
            //normal = n;
            id = _id;
            pSVertices.Add(id,this);

            neighbor = new List<Vertex>();
            face = new List<Triangle>();
        }

        ~Vertex()
        {
            Debug.Assert(face.Count == 0);
            while (neighbor.Count != 0)
            {
                neighbor[0].neighbor.Remove(this);
                neighbor.Remove(neighbor[0]);
            }
            pSVertices.Remove(id);
        }

        public void RemoveIfNonNeighbor(Vertex n)
        {
            // removes n from neighbor list if n isn't a neighbor.
            if (!neighbor.Contains(n)) return;
            for (int i = 0; i < face.Count; i++)
            {
                if (face[i].HasVertex(n)) return;
            }
            neighbor.Remove(n);
        }
    }

}

