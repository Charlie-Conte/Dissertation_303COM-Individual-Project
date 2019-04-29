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

    Vector3[] vertexList;
    int[] indeceList;
    public static List<Vertex> dVList;
    public static List<Triangle> dTList;
    //can have gaps e.g.[ 1, 42, 2 ,0]

    // Start is called before the first frame update
    public void Prepare()
    {
        dVList = new List<Vertex>();
        dTList = new List<Triangle>();
        DecimationManager.vert = new List<Vector3>();
        DecimationManager.tri = new List<TriangleIndeceIDs>();
        newMesh = new Mesh();
        mesh = meshFilter.mesh;
        vertexList = mesh.vertices;
        indeceList = mesh.triangles;


        for (int n = 0; n < vertexList.Length; n++)
        {

            dVList.Add(new Vertex(vertexList[n], mesh.normals[n], n));
            DecimationManager.vert.Add(vertexList[n]);


        }

        for (int m = 2; m < indeceList.Length; m += 3)
        {

            dTList.Add(new Triangle(dVList[indeceList[m - 2]], dVList[indeceList[m - 1]], dVList[indeceList[m]], m));
            DecimationManager.tri.Add(new TriangleIndeceIDs(new int[] {indeceList[m - 2], indeceList[m - 1], indeceList[m]}));

        }





        Debug.Log("Start Done");



    }





    public void Process(List<Vector3> vert, List<TriangleIndeceIDs> tri)
    {


        ComputeAllEdgeCollapseCosts(); // cache all edge collapse costs

                                            // reduce the object down to nothing:
        while (dVList.Count > 0)
        {
            // get the next vertex to collapse
            Vertex mn = MinimumCostEdge();
            int mnID = mn.id;
            int mnIDinList = dVList.FindIndex(vID => vID.id == mnID);

            DecimationManager.permutation[mnIDinList] = dVList.Count - 1;            // keep track of this vertex, i.e. the collapse ordering

            // keep track of vertex to which we collapse to
            DecimationManager.collapse_map[dVList.Count - 1] = (dVList[mnIDinList].collapse != null) ? dVList[mnIDinList].collapse.id : -1;
            // Collapse this edge
            if (dVList[mnIDinList].collapse != null)
            {
                Collapse(mnIDinList, dVList.FindIndex(vID => vID.id == dVList[mnIDinList].collapse.id));
            }
            else
            {
                Collapse(mnIDinList, -1);
            }

        }
        // reorder the map list based on the collapse ordering
        for (int i = 0; i < DecimationManager.collapse_map.Length; i++)
        {
            DecimationManager.collapse_map[i] = (DecimationManager.collapse_map[i] == -1) ? 0 : DecimationManager.permutation[DecimationManager.collapse_map[i]];
        }
        // The caller of this function should reorder their vertices
        // according to the returned "permutation".



    }
    Vertex MinimumCostEdge()
    {
        // Find the edge that when collapsed will affect model the least.
        // This funtion actually returns a Vertex, the second vertex
        // of the edge (collapse candidate) is stored in the vertex data.
        // Serious optimization opportunity here: this function currently
        // does a sequential search through an unsorted list :-(
        // Our algorithm could be O(n*lg(n)) instead of O(n*n)
        Vertex mn = dVList[0];

        for (int i = 0; i < dVList.Count; i++)
        {
            if (dVList[i].objDist < mn.objDist)
            {
                mn = dVList[i];
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
        for (int i = 0; i < dVList.Count; i++)
        {
            ComputeEdgeCostAtVertex(dVList[i]);
        }
    }

    void Collapse(int u,int v)
    {
        // Collapse the edge uv by moving vertex u onto v
        // Actually remove tris on uv, then update tris that
        // have u to have v, and then remove u.
        
        if (v == -1)
        {
            
            // u is a vertex all by itself so just delete it
            
            dVList[u].DeleteVertex();
            
            return;
        }
        int i=0 ;
        List<Vertex >tmp = new List<Vertex>();
        // make tmp a list of all the neighbors of u
        for (i=0;i< dVList[u].neighbor.Count;i++)
        {
            tmp.Add(dVList[u].neighbor[i]);
        }
        // delete triangles on edge uv:
        for (i= dVList[u].face.Count-1;i>=0;i--)
        {
            try
            {
                dVList[u].face[i].HasVertex(dVList[v]);
            }
            catch (Exception)
            {
                Debug.Log(dVList[u].id + "\t" + dVList[u].face[i].id+ "\t" + dVList[v]);
                throw;
            }
            if (dVList[u].face[i].HasVertex(dVList[v]))
            {
                //Debug.Log(i);
                dVList[u].face[i].DeleteTriangle();
                //dVList[dVList.FindIndex(vID => vID.id == dVList[u].face[i].id)].;


            }
        }
        // update remaining triangles to have v instead of u
        for (i = dVList[u].face.Count - 1; i >= 0; i--) 
        {
            dVList[u].face[i].ReplaceVertex(u,v);
        }
        dVList[u].DeleteVertex();
        //dVList.RemoveAt(dVList.FindIndex(vID => vID.id == dVList[u].id));
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
            Debug.Assert(v0 != v1 && v1 != v2 && v2 != v0);  
            id = _id;

            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;

            ComputeNormal();
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
            dTList.Add(this);
        }

        public void DeleteTriangle()
        {
            int i;

            dTList.RemoveAt(dTList.FindIndex(vID => vID.id == id));
            for (i = 0; i < 3; i++)
            {
                if (vertices[i] != null) vertices[i].face.RemoveAt(vertices[i].face.FindIndex(vID => vID.id == id));
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

        /// <summary>
        /// redo for static
        /// </summary>
        /// <param name="vold"></param>
        /// <param name="vnew"></param>
        public void ReplaceVertex(int vold, int vnew)
        {
            //vertices[Array.FindIndex(vertices, v => v.position == vold.position)] = vnew;

            Debug.Assert(vold != -1 && vnew != -1);
            Debug.Assert(dVList[vold] == vertices[0] || dVList[vold] == vertices[1] || dVList[vold] == vertices[2]);
            Debug.Assert(dVList[vnew] != vertices[0] && dVList[vnew] != vertices[1] && dVList[vnew] != vertices[2]);
            if (dVList[vold].id == vertices[0].id)
            {
                vertices[0] = dVList[vnew];
            }
            else if (dVList[vold].id == vertices[1].id)
            {
                vertices[1] = dVList[vnew];
            }
            else
            {
                Debug.Assert(dVList[vold].id == vertices[2].id);
                vertices[2] = dVList[vnew];
            }
            int i;
            //dTList[ dVList[vold].face.FindIndex(fID => fID.id == id)].DeleteTriangle();
            dVList[vold].face.Remove(this);
            Debug.Assert(!dVList[vnew].face.Contains(this));
            dVList[vnew].face.Add(this);
            for (i = 0; i < 3; i++)
            {
                dVList[vold].RemoveIfNonNeighbor(vertices[i]);
                vertices[i].RemoveIfNonNeighbor(dVList[vold]);
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

            return (v.id == vertices[0].id || v.id == vertices[1].id || v.id == vertices[2].id);
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
            //pSVertices.Add(id,this);

            neighbor = new List<Vertex>();
            face = new List<Triangle>();
        }

        public void DeleteVertex()
        {
            Debug.Assert(face.Count == 0);
            while (neighbor.Count != 0)
            {
                neighbor[0].neighbor.Remove(this);
                neighbor.Remove(neighbor[0]);
            }

            dVList.RemoveAt(dVList.FindIndex(vID => vID.id == id));
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

