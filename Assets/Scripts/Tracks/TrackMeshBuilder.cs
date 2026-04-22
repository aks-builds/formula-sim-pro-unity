using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Tracks
{
    /// <summary>
    /// Generates a 3D track mesh at runtime from a CircuitData waypoint list.
    /// Produces: road surface, left/right kerb strips, armco barrier colliders.
    /// Call Build() once after the CircuitData is loaded.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class TrackMeshBuilder : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] CircuitData circuit;

        [Header("Dimensions")]
        [SerializeField] float trackWidth  = 12f;    // half-width each side = 6m
        [SerializeField] float kerbWidth   = 0.8f;
        [SerializeField] float barrierHeight = 0.5f;

        [Header("Materials")]
        [SerializeField] Material trackMaterial;
        [SerializeField] Material kerbMaterial;
        [SerializeField] Material barrierMaterial;

        MeshFilter   mf;
        MeshRenderer mr;
        MeshCollider mc;

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            mc = GetComponent<MeshCollider>();
        }

        void Start()
        {
            if (circuit) Build(circuit);
        }

        public void Build(CircuitData data)
        {
            circuit = data;
            var waypoints = data.waypoints;
            int n = waypoints.Length;
            if (n < 2) return;

            _BuildRoadMesh(waypoints, n);
            _BuildKerbObjects(waypoints, n, true);
            _BuildKerbObjects(waypoints, n, false);
            _BuildBarriers(waypoints, n);
        }

        void _BuildRoadMesh(Vector3[] pts, int n)
        {
            var verts  = new List<Vector3>();
            var uvs    = new List<Vector2>();
            var tris   = new List<int>();

            float uv_v = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector3 fwd = _Forward(pts, i, n);
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized * (trackWidth * 0.5f);

                verts.Add(pts[i] - right);
                verts.Add(pts[i] + right);

                uvs.Add(new Vector2(0f, uv_v));
                uvs.Add(new Vector2(1f, uv_v));

                if (i > 0)
                    uv_v += Vector3.Distance(pts[i], pts[i - 1]) / trackWidth;

                if (i < n - 1)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b, b+2, b+1,  b+1, b+2, b+3 });
                }
            }

            var mesh = new Mesh { name = "TrackRoad" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh    = mesh;
            mr.material = trackMaterial;
            mc.sharedMesh = mesh;
        }

        void _BuildKerbObjects(Vector3[] pts, int n, bool isLeft)
        {
            var obj = new GameObject(isLeft ? "Kerb_L" : "Kerb_R");
            obj.transform.SetParent(transform);

            float side       = isLeft ? -1f : 1f;
            float innerOff   = trackWidth * 0.5f;
            float outerOff   = innerOff + kerbWidth;

            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            for (int i = 0; i < n; i++)
            {
                Vector3 fwd   = _Forward(pts, i, n);
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                verts.Add(pts[i] + right * side * innerOff + Vector3.up * 0.01f);
                verts.Add(pts[i] + right * side * outerOff + Vector3.up * 0.01f);

                uvs.Add(new Vector2(isLeft ? 0 : 1, (float)i / n));
                uvs.Add(new Vector2(isLeft ? 1 : 0, (float)i / n));

                if (i < n - 1)
                {
                    int b = i * 2;
                    if (isLeft) tris.AddRange(new[] { b, b+1, b+2,  b+1, b+3, b+2 });
                    else        tris.AddRange(new[] { b, b+2, b+1,  b+1, b+2, b+3 });
                }
            }

            var mesh = new Mesh { name = isLeft ? "KerbL" : "KerbR" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();

            obj.AddComponent<MeshFilter>().mesh = mesh;
            obj.AddComponent<MeshRenderer>().material = kerbMaterial;
        }

        void _BuildBarriers(Vector3[] pts, int n)
        {
            _SpawnBarrierSide(pts, n, true);
            _SpawnBarrierSide(pts, n, false);
        }

        void _SpawnBarrierSide(Vector3[] pts, int n, bool isLeft)
        {
            float side  = isLeft ? -1f : 1f;
            float offset = trackWidth * 0.5f + kerbWidth + 0.3f;

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 fwd   = _Forward(pts, i, n);
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                Vector3 a = pts[i]     + right * side * offset;
                Vector3 b = pts[i + 1] + right * side * offset;
                float   len = Vector3.Distance(a, b);
                if (len < 0.01f) continue;

                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Barrier_{(isLeft ? "L" : "R")}_{i}";
                seg.transform.SetParent(transform);
                seg.transform.position    = (a + b) * 0.5f + Vector3.up * barrierHeight * 0.5f;
                seg.transform.forward     = (b - a).normalized;
                seg.transform.localScale  = new Vector3(0.3f, barrierHeight, len);
                seg.GetComponent<MeshRenderer>().material = barrierMaterial;
            }
        }

        static Vector3 _Forward(Vector3[] pts, int i, int n)
        {
            Vector3 next = pts[(i + 1) % n];
            Vector3 prev = pts[(i - 1 + n) % n];
            return (next - prev).normalized;
        }
    }
}
