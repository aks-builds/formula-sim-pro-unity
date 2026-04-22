using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.FX
{
    /// <summary>
    /// Procedural skid mark system using a dynamic mesh approach.
    /// Creates a continuous ribbon mesh at each tire contact patch.
    /// Fades over time by reducing vertex alpha.
    /// More performant than decal projectors on mobile.
    /// </summary>
    public class SkidMarkSystem : MonoBehaviour
    {
        public static SkidMarkSystem Instance { get; private set; }

        [Header("Mesh Settings")]
        [SerializeField] Material skidMaterial;
        [SerializeField] float    markWidth      = 0.28f;   // tyre contact patch width (m)
        [SerializeField] float    maxMarks       = 2048;
        [SerializeField] float    minSkidSpeed   = 2f;      // m/s
        [SerializeField] float    fadeTime       = 25f;     // seconds before fully transparent

        [Header("Trigger Thresholds")]
        [SerializeField] float slipAngleThreshold = 0.12f;  // radians
        [SerializeField] float slipRatioThreshold = 0.15f;

        // ── Per-tire mark segment ─────────────────────────────────────────────
        struct MarkSegment
        {
            public Vector3 posL, posR;
            public float   spawnTime;
            public int     meshTriIndex;
        }

        // ── Mesh data ─────────────────────────────────────────────────────────
        Mesh       mesh;
        MeshFilter meshFilter;
        Vector3[]  verts;
        Vector2[]  uvs;
        Color[]    colors;
        int[]      tris;
        int        segHead;    // circular buffer head

        readonly Dictionary<int, Vector3> prevPositions = new();   // tireId → last pos
        float spawnTime;

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
            _InitMesh();
        }

        void _InitMesh()
        {
            int maxVerts = (int)maxMarks * 4;
            verts  = new Vector3[maxVerts];
            uvs    = new Vector2[maxVerts];
            colors = new Color[maxVerts];
            tris   = new int[(int)maxMarks * 6];

            mesh = new Mesh { name = "SkidMarks" };
            mesh.MarkDynamic();

            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.material = skidMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call each FixedUpdate for each tire that is skidding.
        /// tireId: unique int per tire (e.g. 0=FL,1=FR,2=RL,3=RR per car).
        /// </summary>
        public void AddSkidPoint(int tireId, Vector3 worldPos, Vector3 normal,
                                  float slipAngle, float slipRatio, float speedMs)
        {
            bool isSkidding = Mathf.Abs(slipAngle) > slipAngleThreshold
                           || slipRatio > slipRatioThreshold;
            bool isFastEnough = speedMs > minSkidSpeed;

            if (!isSkidding || !isFastEnough)
            {
                prevPositions.Remove(tireId);
                return;
            }

            Vector3 markDir = Vector3.Cross(normal, Vector3.forward).normalized;
            Vector3 right   = markDir * markWidth * 0.5f;

            Vector3 posL = worldPos - right;
            Vector3 posR = worldPos + right;
            posL.z = -0.01f; posR.z = -0.01f;   // slightly above ground

            if (prevPositions.TryGetValue(tireId, out Vector3 prev) &&
                Vector3.Distance(worldPos, prev) < 0.05f)
                return;   // too close — don't add segment

            prevPositions[tireId] = worldPos;

            // Compute segment intensity from slip magnitude
            float intensity = Mathf.Clamp01(
                (Mathf.Abs(slipAngle) / slipAngleThreshold + slipRatio / slipRatioThreshold) * 0.5f);

            _AddSegment(posL, posR, intensity);
        }

        void _AddSegment(Vector3 posL, Vector3 posR, float intensity)
        {
            int vi = (segHead * 4) % verts.Length;
            int ti = (segHead * 6) % tris.Length;

            verts[vi + 0] = posL;
            verts[vi + 1] = posR;
            verts[vi + 2] = posR;
            verts[vi + 3] = posL;

            float u = (segHead % 2) * 0.5f;
            uvs[vi + 0] = new Vector2(u,       0);
            uvs[vi + 1] = new Vector2(u + 0.5f,0);
            uvs[vi + 2] = new Vector2(u + 0.5f,1);
            uvs[vi + 3] = new Vector2(u,       1);

            Color c = new(0.05f, 0.05f, 0.05f, intensity * 0.85f);
            colors[vi + 0] = colors[vi + 1] = colors[vi + 2] = colors[vi + 3] = c;

            tris[ti + 0] = vi; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
            tris[ti + 3] = vi; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;

            segHead++;
            _UploadMesh();
        }

        void Update()
        {
            // Fade all marks over time by reducing alpha
            float dt = Time.deltaTime;
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].a > 0f)
                    colors[i].a = Mathf.Max(0f, colors[i].a - dt / fadeTime);
            }
            mesh.colors = colors;
        }

        void _UploadMesh()
        {
            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.colors    = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
