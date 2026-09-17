using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class DangerousRoadGenerator : MonoBehaviour
{
    [Header("Road Parameters")]
    public int pathSegments = 160;
    public float roadWidth = 3.8f;
    public float segmentLength = 2.5f;
    public float bumpiness = 0.45f;
    public float bumpFrequency = 0.15f;
    public float roadElevationScale = 25f;
    public float roadCurveScale = 45f;

    [Header("Chasm & Bridge Ranges")]
    public Vector2Int bridge1Range = new Vector2Int(45, 60);
    public Vector2Int bridge2Range = new Vector2Int(105, 125);

    [Header("Pothole Segments")]
    public int[] potholeSegments = new int[] { 15, 28, 33, 75, 88, 95, 140 };

    [Header("Materials")]
    public Material roadMaterial;
    public Material rockMaterial;
    public Material woodMaterial;
    public Material cableMaterial;

    [HideInInspector]
    public List<Vector3> pathPoints = new List<Vector3>();
    [HideInInspector]
    public List<Vector3> pathForwards = new List<Vector3>();
    [HideInInspector]
    public List<Vector3> pathRights = new List<Vector3>();

    public void GenerateCompleteEnvironment()
    {
        ClearExisting();
        CreateMaterials();
        ComputeSplinePath();
        BuildRoadMesh();
        BuildCanyonWalls();
        BuildPlankBridges();
        BuildPotholesAndDebris();
        BuildEnvironmentDecorations();
        BuildStartAndFinishGates();
        BuildCanyonMistLayer();
    }

    private void ClearExisting()
    {
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            children.Add(transform.GetChild(i).gameObject);
        }
        foreach (var c in children)
        {
            DestroyImmediate(c);
        }
    }

    private void CreateMaterials()
    {
        Shader standardShader = Shader.Find("Standard");
        if (standardShader == null) standardShader = Shader.Find("Diffuse");

        if (roadMaterial == null)
        {
            roadMaterial = new Material(standardShader);
            roadMaterial.name = "Mat_CrackedRoad";
            roadMaterial.color = new Color(0.25f, 0.25f, 0.26f);
            roadMaterial.SetFloat("_Glossiness", 0.12f);
            roadMaterial.mainTexture = GenerateAsphaltTexture();
        }

        if (rockMaterial == null)
        {
            rockMaterial = new Material(standardShader);
            rockMaterial.name = "Mat_CanyonRock";
            rockMaterial.color = new Color(0.48f, 0.40f, 0.32f);
            rockMaterial.SetFloat("_Glossiness", 0.05f);
            rockMaterial.mainTexture = GenerateRockTexture();
        }

        if (woodMaterial == null)
        {
            woodMaterial = new Material(standardShader);
            woodMaterial.name = "Mat_WeatheredWood";
            woodMaterial.color = new Color(0.55f, 0.38f, 0.22f);
            woodMaterial.SetFloat("_Glossiness", 0.08f);
            woodMaterial.mainTexture = GenerateWoodTexture();
        }

        if (cableMaterial == null)
        {
            cableMaterial = new Material(standardShader);
            cableMaterial.name = "Mat_RustyCable";
            cableMaterial.color = new Color(0.2f, 0.18f, 0.16f);
            cableMaterial.SetFloat("_Glossiness", 0.3f);
        }
    }

    private void ComputeSplinePath()
    {
        pathPoints.Clear();
        pathForwards.Clear();
        pathRights.Clear();

        for (int i = 0; i <= pathSegments; i++)
        {
            float t = (float)i / pathSegments;
            float z = i * segmentLength;

            float x = Mathf.Sin(t * Mathf.PI * 4f) * roadCurveScale * 0.7f
                    + Mathf.Sin(t * Mathf.PI * 9f) * (roadCurveScale * 0.3f)
                    + Mathf.Cos(t * Mathf.PI * 2.5f) * 15f;

            float y = Mathf.Sin(t * Mathf.PI * 2.2f) * roadElevationScale
                    + Mathf.Cos(t * Mathf.PI * 5f) * 6f;

            bool isBridge1 = (i >= bridge1Range.x && i <= bridge1Range.y);
            bool isBridge2 = (i >= bridge2Range.x && i <= bridge2Range.y);

            if (!isBridge1 && !isBridge2)
            {
                float microNoise = (Mathf.PerlinNoise(i * bumpFrequency, 42.1f) - 0.5f) * bumpiness * 2.5f;
                y += microNoise;
            }

            pathPoints.Add(new Vector3(x, y, z));
        }

        for (int i = 0; i <= pathSegments; i++)
        {
            Vector3 forward;
            if (i < pathSegments)
                forward = (pathPoints[i + 1] - pathPoints[i]).normalized;
            else
                forward = (pathPoints[i] - pathPoints[i - 1]).normalized;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            pathForwards.Add(forward);
            pathRights.Add(right);
        }
    }

    private void BuildRoadMesh()
    {
        GameObject roadRoot = new GameObject("RoadMesh_Terrain");
        roadRoot.transform.SetParent(transform, false);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i < pathSegments; i++)
        {
            if ((i >= bridge1Range.x && i < bridge1Range.y) || (i >= bridge2Range.x && i < bridge2Range.y))
            {
                continue;
            }

            Vector3 p0 = pathPoints[i];
            Vector3 p1 = pathPoints[i + 1];
            Vector3 r0 = pathRights[i];
            Vector3 r1 = pathRights[i + 1];

            bool hasPothole = false;
            foreach (int ph in potholeSegments)
            {
                if (i == ph || i == ph + 1) { hasPothole = true; break; }
            }

            float currentWidth = roadWidth;
            if (Mathf.Abs(i - bridge1Range.x) < 4 || Mathf.Abs(i - bridge2Range.x) < 4)
            {
                currentWidth *= 0.8f;
            }

            float halfW = currentWidth * 0.5f;
            float dip = hasPothole ? -0.45f : 0f;

            Vector3 v0 = p0 - r0 * halfW;
            Vector3 v1 = p0 - r0 * (halfW * 0.3f) + Vector3.up * dip;
            Vector3 v2 = p0 + r0 * (halfW * 0.3f) + Vector3.up * (hasPothole ? -0.2f : 0f);
            Vector3 v3 = p0 + r0 * halfW;

            Vector3 nv0 = p1 - r1 * halfW;
            Vector3 nv1 = p1 - r1 * (halfW * 0.3f) + Vector3.up * dip;
            Vector3 nv2 = p1 + r1 * (halfW * 0.3f) + Vector3.up * (hasPothole ? -0.2f : 0f);
            Vector3 nv3 = p1 + r1 * halfW;

            Vector3[] slice0 = new Vector3[] { v0, v1, v2, v3 };
            Vector3[] slice1 = new Vector3[] { nv0, nv1, nv2, nv3 };

            int baseIdx = verts.Count;
            for (int k = 0; k < 4; k++)
            {
                verts.Add(slice0[k]);
                uvs.Add(new Vector2((float)k / 3f, i * 0.5f));
            }
            for (int k = 0; k < 4; k++)
            {
                verts.Add(slice1[k]);
                uvs.Add(new Vector2((float)k / 3f, (i + 1) * 0.5f));
            }

            for (int k = 0; k < 3; k++)
            {
                int bl = baseIdx + k;
                int br = baseIdx + k + 1;
                int tl = baseIdx + 4 + k;
                int tr = baseIdx + 4 + k + 1;

                tris.Add(bl); tris.Add(tl); tris.Add(tr);
                tris.Add(bl); tris.Add(tr); tris.Add(br);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "DangerousRoad_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter mf = roadRoot.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = roadRoot.AddComponent<MeshRenderer>();
        mr.sharedMaterial = roadMaterial;

        MeshCollider mc = roadRoot.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    private void BuildCanyonWalls()
    {
        GameObject canyonRoot = new GameObject("CanyonAndCliffs");
        canyonRoot.transform.SetParent(transform, false);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float cliffDepth = 80f;
        float cliffSpread = 40f;

        for (int i = 0; i < pathSegments; i++)
        {
            Vector3 p0 = pathPoints[i];
            Vector3 p1 = pathPoints[i + 1];
            Vector3 r0 = pathRights[i];
            Vector3 r1 = pathRights[i + 1];

            float halfW = roadWidth * 0.5f;

            Vector3 leftTop0 = p0 - r0 * halfW;
            Vector3 leftTop1 = p1 - r1 * halfW;
            Vector3 leftBot0 = p0 - r0 * (halfW + cliffSpread) - Vector3.up * cliffDepth;
            Vector3 leftBot1 = p1 - r1 * (halfW + cliffSpread) - Vector3.up * cliffDepth;

            Vector3 rightTop0 = p0 + r0 * halfW;
            Vector3 rightTop1 = p1 + r1 * halfW;
            Vector3 rightBot0 = p0 + r0 * (halfW + cliffSpread) - Vector3.up * cliffDepth;
            Vector3 rightBot1 = p1 + r1 * (halfW + cliffSpread) - Vector3.up * cliffDepth;

            leftBot0 += new Vector3(Mathf.Sin(i * 0.8f) * 4f, 0, Mathf.Cos(i * 0.7f) * 4f);
            rightBot0 += new Vector3(Mathf.Cos(i * 0.6f) * 5f, 0, Mathf.Sin(i * 0.9f) * 5f);

            int bIdx = verts.Count;
            verts.Add(leftTop0); verts.Add(leftTop1);
            verts.Add(leftBot1); verts.Add(leftBot0);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(1, 4)); uvs.Add(new Vector2(0, 4));

            tris.Add(bIdx); tris.Add(bIdx + 1); tris.Add(bIdx + 2);
            tris.Add(bIdx); tris.Add(bIdx + 2); tris.Add(bIdx + 3);

            bIdx = verts.Count;
            verts.Add(rightTop0); verts.Add(rightBot0);
            verts.Add(rightBot1); verts.Add(rightTop1);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 4));
            uvs.Add(new Vector2(1, 4)); uvs.Add(new Vector2(1, 0));

            tris.Add(bIdx); tris.Add(bIdx + 1); tris.Add(bIdx + 2);
            tris.Add(bIdx); tris.Add(bIdx + 2); tris.Add(bIdx + 3);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Canyon_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter mf = canyonRoot.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = canyonRoot.AddComponent<MeshRenderer>();
        mr.sharedMaterial = rockMaterial;

        MeshCollider mc = canyonRoot.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    private void BuildPlankBridges()
    {
        GameObject bridgesRoot = new GameObject("RicketyPlankBridges");
        bridgesRoot.transform.SetParent(transform, false);

        BuildSinglePlankBridge(bridgesRoot, bridge1Range.x, bridge1Range.y, "PlankBridge_DoubleTrack", true);
        BuildSinglePlankBridge(bridgesRoot, bridge2Range.x, bridge2Range.y, "PlankBridge_NarrowSingle", false);
    }

    private void BuildSinglePlankBridge(GameObject parent, int startIdx, int endIdx, string name, bool isDoubleTrack)
    {
        GameObject bridge = new GameObject(name);
        bridge.transform.SetParent(parent.transform, false);

        Vector3 startPt = pathPoints[startIdx];
        Vector3 endPt = pathPoints[endIdx];
        float totalDist = Vector3.Distance(startPt, endPt);

        int plankCount = Mathf.RoundToInt(totalDist / 0.45f);

        for (int i = 0; i < plankCount; i++)
        {
            float t = (float)i / plankCount;
            float sag = Mathf.Sin(t * Mathf.PI) * -1.2f;

            int approxIdx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(startIdx, endIdx, t)), 0, pathSegments);
            Vector3 center = pathPoints[approxIdx] + Vector3.up * sag;
            Vector3 fwd = pathForwards[approxIdx];
            Vector3 right = pathRights[approxIdx];

            if (i % 8 == 0 && i > 3 && i < plankCount - 3)
            {
                continue; // Missing plank gap
            }

            float tiltAngle = Mathf.Sin(i * 1.7f) * 6.5f;
            float yawAngle = Mathf.Cos(i * 2.3f) * 4f;

            // Her 6 tahtadan biri çürük ve kırılabilir olsun
            bool isRotten = (i % 6 == 3 && i > 2 && i < plankCount - 2);

            if (isDoubleTrack)
            {
                CreatePlank(bridge.transform, center - right * 0.95f, fwd, right, 0.9f, 0.32f, 0.08f, tiltAngle, yawAngle, isRotten);
                CreatePlank(bridge.transform, center + right * 0.95f, fwd, right, 0.9f, 0.32f, 0.08f, -tiltAngle, yawAngle, isRotten);
            }
            else
            {
                float width = 2.4f + Mathf.Sin(i * 0.9f) * 0.3f;
                CreatePlank(bridge.transform, center, fwd, right, width, 0.34f, 0.09f, tiltAngle, yawAngle, isRotten);
            }
        }

        CreateBridgeCables(bridge.transform, startIdx, endIdx);
    }

    private void CreatePlank(Transform parent, Vector3 pos, Vector3 fwd, Vector3 right, float width, float length, float thickness, float tilt, float yaw, bool isRotten)
    {
        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = isRotten ? "Plank_Rotten" : "Plank";
        plank.transform.SetParent(parent, true);
        plank.transform.position = pos;

        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
        rot *= Quaternion.Euler(tilt, yaw, 0);
        plank.transform.rotation = rot;

        plank.transform.localScale = new Vector3(width, thickness, length);

        Renderer r = plank.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = woodMaterial;

        // Dinamik sallanma & kırılma bileşeni
        DynamicPlank dp = plank.AddComponent<DynamicPlank>();
        dp.isRotten = isRotten;
    }

    private void CreateBridgeCables(Transform parent, int startIdx, int endIdx)
    {
        GameObject cableGroup = new GameObject("CablesAndBeams");
        cableGroup.transform.SetParent(parent, false);

        int count = endIdx - startIdx;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < count; i++)
            {
                int idx0 = startIdx + i;
                int idx1 = startIdx + i + 1;

                float t0 = (float)i / count;
                float t1 = (float)(i + 1) / count;
                float sag0 = Mathf.Sin(t0 * Mathf.PI) * -1.2f - 0.2f;
                float sag1 = Mathf.Sin(t1 * Mathf.PI) * -1.2f - 0.2f;

                Vector3 p0 = pathPoints[idx0] + pathRights[idx0] * (side * 1.3f) + Vector3.up * sag0;
                Vector3 p1 = pathPoints[idx1] + pathRights[idx1] * (side * 1.3f) + Vector3.up * sag1;

                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "CableSegment";
                beam.transform.SetParent(cableGroup.transform, true);

                Vector3 mid = (p0 + p1) * 0.5f;
                Vector3 dir = (p1 - p0);
                beam.transform.position = mid;
                beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                beam.transform.localScale = new Vector3(0.08f, dir.magnitude * 0.5f, 0.08f);

                Renderer r = beam.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = cableMaterial;

                Collider c = beam.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
            }
        }
    }

    private void BuildPotholesAndDebris()
    {
        GameObject potholeRoot = new GameObject("PotholesAndRocks");
        potholeRoot.transform.SetParent(transform, false);

        foreach (int ph in potholeSegments)
        {
            if (ph >= pathSegments) continue;

            Vector3 pt = pathPoints[ph];
            Vector3 right = pathRights[ph];

            int rockCount = Random.Range(2, 5);
            for (int r = 0; r < rockCount; r++)
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "FallenRock";
                rock.transform.SetParent(potholeRoot.transform, true);

                float offset = (Random.value - 0.5f) * (roadWidth * 0.8f);
                rock.transform.position = pt + right * offset + Vector3.up * 0.25f;
                rock.transform.localScale = new Vector3(
                    Random.Range(0.4f, 0.9f),
                    Random.Range(0.3f, 0.7f),
                    Random.Range(0.4f, 0.9f)
                );
                rock.transform.rotation = Random.rotation;

                Renderer rend = rock.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = rockMaterial;
            }

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "WarningStake";
            marker.transform.SetParent(potholeRoot.transform, true);
            marker.transform.position = pt - right * (roadWidth * 0.55f) + Vector3.up * 0.75f;
            marker.transform.localScale = new Vector3(0.12f, 1.5f, 0.12f);
            marker.transform.rotation = Quaternion.Euler(0, 0, 12f);
            Renderer mr = marker.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial = woodMaterial;
        }
    }

    private void BuildEnvironmentDecorations()
    {
        GameObject decoRoot = new GameObject("CliffsWarningPosts");
        decoRoot.transform.SetParent(transform, false);

        for (int i = 10; i < pathSegments - 10; i += 6)
        {
            if ((i >= bridge1Range.x - 3 && i <= bridge1Range.y + 3) ||
                (i >= bridge2Range.x - 3 && i <= bridge2Range.y + 3))
            {
                continue;
            }

            Vector3 pt = pathPoints[i];
            Vector3 right = pathRights[i];

            for (int side = -1; side <= 1; side += 2)
            {
                if (Random.value > 0.45f) continue;

                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "CliffPost";
                post.transform.SetParent(decoRoot.transform, true);
                post.transform.position = pt + right * (side * (roadWidth * 0.55f)) + Vector3.up * 0.4f;
                post.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
                post.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), 0, Random.Range(-10f, 10f));

                Renderer rend = post.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = woodMaterial;
            }
        }
    }

    private void BuildStartAndFinishGates()
    {
        GameObject gatesRoot = new GameObject("RaceGates");
        gatesRoot.transform.SetParent(transform, false);

        // 1. Başlangıç Takı
        if (pathPoints.Count > 1)
        {
            Vector3 startPos = pathPoints[1];
            Vector3 startFwd = pathForwards[1];
            Vector3 startRight = pathRights[1];
            CreateGate(gatesRoot.transform, startPos, startFwd, startRight, "START", new Color(0.2f, 0.8f, 0.3f));
        }

        // 2. Bitiş Takı (Zirve)
        if (pathPoints.Count > 4)
        {
            int finishIdx = pathSegments - 3;
            Vector3 finishPos = pathPoints[finishIdx];
            Vector3 finishFwd = pathForwards[finishIdx];
            Vector3 finishRight = pathRights[finishIdx];
            GameObject finishGate = CreateGate(gatesRoot.transform, finishPos, finishFwd, finishRight, "FINISH", new Color(1f, 0.85f, 0.2f));

            // Bitiş Trigger'ı
            BoxCollider bc = finishGate.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(roadWidth * 1.5f, 4f, 2f);
            bc.center = new Vector3(0, 2f, 0);
            finishGate.AddComponent<FinishTrigger>();
        }
    }

    private GameObject CreateGate(Transform parent, Vector3 pos, Vector3 fwd, Vector3 right, string label, Color bannerColor)
    {
        GameObject gate = new GameObject("Gate_" + label);
        gate.transform.SetParent(parent, false);
        gate.transform.position = pos;
        gate.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        Shader s = Shader.Find("Standard");
        Material postMat = new Material(s);
        postMat.color = new Color(0.3f, 0.25f, 0.2f);

        Material bannerMat = new Material(s);
        bannerMat.color = bannerColor;

        // Sol Direk
        GameObject pL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pL.transform.SetParent(gate.transform, false);
        pL.transform.localPosition = new Vector3(-roadWidth * 0.65f, 2f, 0);
        pL.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
        pL.GetComponent<Renderer>().sharedMaterial = postMat;

        // Sağ Direk
        GameObject pR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pR.transform.SetParent(gate.transform, false);
        pR.transform.localPosition = new Vector3(roadWidth * 0.65f, 2f, 0);
        pR.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
        pR.GetComponent<Renderer>().sharedMaterial = postMat;

        // Üst Kiriş
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.transform.SetParent(gate.transform, false);
        beam.transform.localPosition = new Vector3(0, 4.1f, 0);
        beam.transform.localScale = new Vector3(roadWidth * 1.4f, 0.45f, 0.45f);
        beam.GetComponent<Renderer>().sharedMaterial = bannerMat;

        return gate;
    }

    private void BuildCanyonMistLayer()
    {
        // Kanyonun dibinde derinlik hissi veren büyük bir sis düzlemi
        GameObject mist = GameObject.CreatePrimitive(PrimitiveType.Plane);
        mist.name = "Canyon_Mist_Floor";
        mist.transform.SetParent(transform, false);
        mist.transform.position = new Vector3(0, -65f, pathSegments * segmentLength * 0.5f);
        mist.transform.localScale = new Vector3(80f, 1f, 80f);

        Shader s = Shader.Find("Standard");
        Material mistMat = new Material(s);
        mistMat.color = new Color(0.7f, 0.72f, 0.75f, 0.6f);
        mist.GetComponent<Renderer>().sharedMaterial = mistMat;
        Collider c = mist.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c);
    }

    private Texture2D GenerateAsphaltTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.25f;
                float fine = Mathf.PerlinNoise(x * 0.8f, y * 0.8f) * 0.15f;
                float val = 0.22f + n + fine;
                cols[y * size + x] = new Color(val, val, val * 0.98f, 1f);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    private Texture2D GenerateRockTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n1 = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float n2 = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.4f;
                float val = 0.35f + n1 * 0.3f + n2;
                cols[y * size + x] = new Color(val * 1.1f, val * 0.95f, val * 0.8f, 1f);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    private Texture2D GenerateWoodTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.Sin(x * 0.4f + Mathf.PerlinNoise(x * 0.05f, y * 0.01f) * 10f);
                float baseVal = 0.45f + grain * 0.12f;
                cols[y * size + x] = new Color(baseVal * 1.2f, baseVal * 0.9f, baseVal * 0.55f, 1f);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }
}