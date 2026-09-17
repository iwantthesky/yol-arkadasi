using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DortCuce.UnityGame
{
    /// <summary>Visual road follows the same samples and obstacle footprints as MotorSimulation.</summary>
    public sealed class MotorWorld : MonoBehaviour
    {
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, GameObject> nature = new Dictionary<string, GameObject>();
        private readonly List<MovingObstacle> moving = new List<MovingObstacle>();
        private readonly List<Transform> windsocks = new List<Transform>();
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        private Transform scenery;
        private Font font;
        private bool built;

        public int AntigravityInstances { get; private set; }
        public int MountainColliders { get; private set; }
        public int TreeColliders { get; private set; }

        private static readonly Color Asphalt = new Color(0.22f, 0.29f, 0.29f);
        private static readonly Color Ivory = new Color(0.94f, 0.89f, 0.74f);
        private static readonly Color Orange = new Color(0.90f, 0.36f, 0.17f);
        private static readonly Color Teal = new Color(0.08f, 0.21f, 0.23f);
        private static readonly Color Sage = new Color(0.37f, 0.46f, 0.32f);
        private static readonly Color Sand = new Color(0.62f, 0.46f, 0.30f);
        private static readonly Color Clay = new Color(0.53f, 0.29f, 0.19f);
        private static readonly Color Wood = new Color(0.39f, 0.24f, 0.14f);

        private struct MovingObstacle
        {
            public CourseObstacle obstacle;
            public Transform root;
        }

        public static Vector3 Point(float distance, float lateral = 0f)
        {
            var sample = MotorCourse.Sample(distance);
            return new Vector3(sample.centerX + lateral, sample.height, distance);
        }

        public void Build()
        {
            if (built) return;
            built = true;
            MotorCourse.EnvironmentPhysicsReady = false;
            ValidateAntigravityAssets();
            scenery = new GameObject("Antigravity mountain road · shared course geometry").transform;
            scenery.SetParent(transform, false);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ConfigureLight();
            BuildRoad();
            BuildTerrain();
            BuildBridge();
            BuildRouteFurniture();
            BuildObstacles();
            BuildBasecamp(0f, false);
            BuildBasecamp(MotorCourse.Length, true);
            Decorate();
            Physics.SyncTransforms();
            MotorCourse.EnvironmentPhysicsReady = true;
            Debug.Log("ANTIGRAVITY_WORLD_READY instances=" + AntigravityInstances
                + " mountainColliders=" + MountainColliders + " treeColliders=" + TreeColliders);
        }

        public void Animate(float elapsed)
        {
            foreach (var item in moving)
                item.root.position = Point(item.obstacle.distance, MotorCourse.ObstacleLateral(item.obstacle, elapsed));
            for (int i = 0; i < windsocks.Count; i++)
            {
                var sock = windsocks[i];
                sock.localRotation = Quaternion.Euler(0f, 12f * Mathf.Sin(elapsed * 1.8f + i), 77f + 9f * Mathf.Sin(elapsed * 3f + i));
            }
        }

        private void ConfigureLight()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = new Color(0.64f, 0.72f, 0.78f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.76f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.54f, 0.57f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.22f, 0.17f);
            RenderSettings.reflectionIntensity = 0.45f;
            var sunlight = new GameObject("Late afternoon sun").AddComponent<Light>();
            sunlight.transform.SetParent(transform, false);
            sunlight.transform.rotation = Quaternion.Euler(36f, -38f, 0f);
            sunlight.type = LightType.Directional;
            sunlight.color = new Color(1f, 0.86f, 0.65f);
            sunlight.intensity = 1.25f;
            sunlight.shadows = LightShadows.Soft;
            sunlight.shadowStrength = 0.76f;
            sunlight.shadowBias = 0.045f;
            RenderSettings.sun = sunlight;
            QualitySettings.shadowDistance = 180f;
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "Warm mountain sky" };
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetColor("_SkyTint", new Color(0.39f, 0.58f, 0.76f));
                sky.SetColor("_GroundColor", new Color(0.48f, 0.39f, 0.29f));
                sky.SetFloat("_Exposure", 1.18f);
                RenderSettings.skybox = sky;
                materials.Add("sky", sky);
            }
        }

        private void BuildRoad()
        {
            // A continuous sampled mesh avoids seams and matches the simulation exactly.
            BuildRibbon("Asphalt · first half", -18f, 400f, 2f, (d, s) => -s.width * 0.5f, (d, s) => s.width * 0.5f, 0f, Mat("road", Asphalt));
            BuildRibbon("Asphalt · mountain pass", 455f, MotorCourse.Length + 20f, 2f, (d, s) => -s.width * 0.5f, (d, s) => s.width * 0.5f, 0f, Mat("road", Asphalt));
            BuildRibbon("Bridge deck", 400f, 455f, 1f, (d, s) => -s.width * 0.5f, (d, s) => s.width * 0.5f, 0f, Mat("wood", Wood));
            for (int side = -1; side <= 1; side += 2)
            {
                int fixedSide = side;
                BuildRibbon("Ivory edge line " + side, -15f, MotorCourse.Length + 15f, 2f,
                    (d, s) => fixedSide * (s.width * 0.5f - 0.20f) - 0.065f,
                    (d, s) => fixedSide * (s.width * 0.5f - 0.20f) + 0.065f, 0.013f, Mat("ivory", Ivory));
                BuildRibbon("Gravel verge " + side, -18f, MotorCourse.Length + 20f, 2f,
                    (d, s) => fixedSide > 0 ? s.width * 0.5f : -s.width * 0.5f - 0.75f,
                    (d, s) => fixedSide > 0 ? s.width * 0.5f + 0.75f : -s.width * 0.5f,
                    -0.04f, Mat("gravel", new Color(0.69f, 0.59f, 0.43f)), true);
            }
            for (float d = 16f; d < MotorCourse.Length - 12f; d += 13f)
            {
                if (d > 382f && d < 465f) continue;
                var p = Point(d) + Vector3.up * 0.018f;
                var dash = Box("Centre dash", p, new Vector3(0.10f, 0.015f, 3.8f), Mat("ivory", Ivory));
                dash.rotation = RoadRotation(d);
            }
            // The arrival apron keeps the final stopping area readable.
            Box("Start apron", Point(-9f) + Vector3.down * 0.22f, new Vector3(17f, 0.40f, 20f), Mat("road", Asphalt));
            Box("Finish apron", Point(909f) + Vector3.down * 0.22f, new Vector3(17f, 0.40f, 20f), Mat("road", Asphalt));
        }

        private delegate float Edge(float distance, CourseSample sample);

        private void BuildRibbon(string name, float start, float end, float step, Edge left, Edge right, float lift, Material material, bool bridgeGap = false)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (float z = start; z < end - 0.001f; z += step)
            {
                float next = Mathf.Min(z + step, end);
                if (bridgeGap && z >= 398f && z < 457f) continue;
                var a = MotorCourse.Sample(z);
                var b = MotorCourse.Sample(next);
                AddQuad(vertices, triangles,
                    new Vector3(a.centerX + left(z, a), a.height + lift, z),
                    new Vector3(a.centerX + right(z, a), a.height + lift, z),
                    new Vector3(b.centerX + right(next, b), b.height + lift, next),
                    new Vector3(b.centerX + left(next, b), b.height + lift, next));
            }
            MeshObject(name, vertices, triangles, material);
        }

        private void BuildTerrain()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var grassVertices = new List<Vector3>();
                var grassIndices = new List<int>();
                var cliffVertices = new List<Vector3>();
                var cliffIndices = new List<int>();
                var outerVertices = new List<Vector3>();
                var outerIndices = new List<int>();
                for (float z = MotorCourse.MapMinZ; z < MotorCourse.MapMaxZ; z += 10f)
                {
                    var a = MotorCourse.Sample(z);
                    var b = MotorCourse.Sample(z + 10f);
                    float gapA = CanyonDepth(z);
                    float gapB = CanyonDepth(z + 10f);
                    var nearA = new Vector3(a.centerX + side * (a.width * 0.5f + 0.70f), a.height - 0.08f - gapA, z);
                    var nearB = new Vector3(b.centerX + side * (b.width * 0.5f + 0.70f), b.height - 0.08f - gapB, z + 10f);
                    var midA = new Vector3(a.centerX + side * 26f, a.height - 1.8f - gapA + Noise(z, side) * 3.8f, z);
                    var midB = new Vector3(b.centerX + side * 26f, b.height - 1.8f - gapB + Noise(z + 10f, side) * 3.8f, z + 10f);
                    var farA = new Vector3(a.centerX + side * MotorCourse.RidgeWidth, a.height - 8f - gapA + Noise(z, side + 8) * 12f, z);
                    var farB = new Vector3(b.centerX + side * MotorCourse.RidgeWidth, b.height - 8f - gapB + Noise(z + 10f, side + 8) * 12f, z + 10f);
                    float outerXA = a.centerX + side * MotorCourse.MapHalfWidth;
                    float outerXB = b.centerX + side * MotorCourse.MapHalfWidth;
                    var outerA = new Vector3(outerXA, MotorCourse.GroundHeight(z, outerXA), z);
                    var outerB = new Vector3(outerXB, MotorCourse.GroundHeight(z + 10f, outerXB), z + 10f);
                    AddQuadUp(grassVertices, grassIndices, nearA, nearB, midB, midA);
                    AddQuadUp(cliffVertices, cliffIndices, midA, midB, farB, farA);
                    AddQuadUp(outerVertices, outerIndices, farA, farB, outerB, outerA);
                }
                MeshObject("Sage roadside " + side, grassVertices, grassIndices, Mat("sage", Sage));
                MeshObject("Terracotta ravine " + side, cliffVertices, cliffIndices, Mat("sand", Sand));
                MeshObject("Open riding terrain " + side, outerVertices, outerIndices, Mat("outerTerrain", new Color(0.34f, 0.43f, 0.25f)));
            }
            BuildAntigravityMountainRange();
            float riverY = MotorCourse.Sample(427f).height - 10.5f;
            Box("River beneath bridge", new Vector3(MotorCourse.Sample(427f).centerX, riverY, 427f), new Vector3(155f, 0.08f, 15f), Mat("water", new Color(0.25f, 0.54f, 0.54f), 0.40f));
            for (int i = 0; i < 13; i++)
                Box("River glint", new Vector3(MotorCourse.Sample(427f).centerX - 66f + i * 11f, riverY + 0.07f, 425f + Mathf.Sin(i * 2.1f) * 4f), new Vector3(3.5f, 0.02f, 0.20f), Mat("foam", new Color(0.69f, 0.81f, 0.72f)));
        }

        private static float CanyonDepth(float z)
        {
            return 11f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(386f, 405f, z))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(450f, 471f, z)));
        }

        private void BuildBridge()
        {
            for (float d = 400f; d <= 455f; d += 2f)
            {
                Antigravity("Props/05_Duz_Uzun_Tahta", Point(d) + Vector3.up * 0.015f,
                    RoadRotation(d) * Quaternion.Euler(0f, 0f, (d % 4f < 2f ? -1f : 1f) * 0.8f), 0.90f);
            }
            Antigravity("Props/07_Ahsap_Yol_Barikati", Point(397f, -4.5f), RoadRotation(397f), 0.85f);
            Antigravity("Props/07_Ahsap_Yol_Barikati", Point(458f, 4.5f), RoadRotation(458f) * Quaternion.Euler(0f, 180f, 0f), 0.85f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (float d = 400f; d <= 455f; d += 5f)
                {
                    float lateral = side * (MotorCourse.Sample(d).width * 0.5f + 0.20f);
                    var p = Point(d, lateral);
                    Box("Bridge rail post", p + Vector3.up * 0.55f, new Vector3(0.14f, 1.35f, 0.17f), Mat("wood", Wood));
                    if (d < 455f)
                    {
                        float next = Mathf.Min(d + 5f, 455f);
                        float nextLateral = side * (MotorCourse.Sample(next).width * 0.5f + 0.20f);
                        Beam("Bridge rope rail", p + Vector3.up * 1.10f, Point(next, nextLateral) + Vector3.up * 1.10f, 0.055f, Mat("rope", new Color(0.72f, 0.63f, 0.43f)));
                        Beam("Bridge underside beam", p + Vector3.down * 0.30f, Point(next, nextLateral) + Vector3.down * 0.30f, 0.22f, Mat("wood", Wood));
                    }
                    if (((int)d - 400) % 15 == 0)
                        Box("Bridge trestle", p + Vector3.down * 5.5f, new Vector3(0.32f, 11f, 0.40f), Mat("wood", Wood));
                }
            }
        }

        private void BuildRouteFurniture()
        {
            // Checkpoint gates use the simulation's actual checkpoint distances.
            for (int i = 1; i < MotorCourse.Checkpoints.Length; i++)
            {
                float d = MotorCourse.Checkpoints[i];
                float width = MotorCourse.Sample(d).width;
                var gate = Group("Checkpoint " + i, Point(d), RoadRotation(d));
                for (int side = -1; side <= 1; side += 2)
                {
                    LocalBox("Checkpoint post", gate, new Vector3(side * (width * 0.5f + 0.7f), 2.1f, 0f), new Vector3(0.23f, 4.2f, 0.23f), Mat("orange", Orange));
                    LocalBox("Checkpoint foot", gate, new Vector3(side * (width * 0.5f + 0.7f), 0.24f, 0f), new Vector3(0.75f, 0.48f, 0.8f), Mat("ivory", Ivory));
                }
                LocalBox("Checkpoint banner", gate, new Vector3(0f, 4.12f, 0f), new Vector3(width + 1.8f, 0.65f, 0.19f), Mat("teal", Teal));
                Text("MOLA " + i.ToString("00") + "  /  " + ((int)d) + " M", gate, new Vector3(0f, 4.14f, -0.11f), 0.19f, Ivory);
                for (int j = 0; j < 10; j++)
                    LocalBox("Checkpoint timing stripe", gate, new Vector3(-width * 0.5f + (j + 0.5f) * width / 10f, 0.02f, 0f), new Vector3(width / 10f, 0.015f, 0.55f), j % 2 == 0 ? Mat("ivory", Ivory) : Mat("teal", Teal));
            }
            Sign(35f, 1, "YOL ARKADAŞI", "GAZ • VİTES • DENGE", false);
            Sign(133f, -1, "VİRAJLAR", "BİRLİKTE HAFİFÇE YATIN", true);
            Sign(282f, 1, "YAN RÜZGÂR", "DENGE EKİBİ HAZIR MI?", true);
            Sign(373f, -1, "ASMA KÖPRÜ", "DÜŞÜK VİTES • SABİT GAZ", true);
            Sign(463f, 1, "RAMPA", "ÖNCE HIZ • SONRA DENGE", true);
            Sign(658f, -1, "SON TIRMANIŞ", "VİTESİ ERKEN KÜÇÜLTÜN", true);
            Sign(832f, 1, "SON DURAK", "ÇAY 70 METRE İLERİDE", false);
            for (float d = 15f; d < MotorCourse.Length; d += 18f)
            {
                if (d > 394f && d < 461f) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = Point(d, side * (MotorCourse.Sample(d).width * 0.5f + 0.50f));
                    Box("Road reflector", p + Vector3.up * 0.49f, new Vector3(0.13f, 0.94f, 0.13f), Mat("ivory", Ivory));
                    Box("Orange reflector cap", p + Vector3.up * 0.78f, new Vector3(0.15f, 0.22f, 0.15f), Mat("orange", Orange));
                }
            }
            for (float d = 306f; d <= 370f; d += 30f)
            {
                var p = Point(d, MotorCourse.Sample(d).width * 0.5f + 2f);
                Box("Windsock mast", p + Vector3.up * 2.5f, new Vector3(0.10f, 5f, 0.10f), Mat("ivory", Ivory));
                var pivot = Group("Windsock pivot", p + Vector3.up * 4.6f, Quaternion.identity);
                var sock = Group("Windsock", Vector3.zero, Quaternion.identity, pivot, true);
                for (int j = 0; j < 5; j++)
                    LocalBox("Windsock fabric", sock, new Vector3(0f, j * 0.24f, 0f), new Vector3(0.42f - j * 0.06f, 0.25f, 0.42f - j * 0.06f), j % 2 == 0 ? Mat("orange", Orange) : Mat("ivory", Ivory));
                windsocks.Add(sock);
            }
        }

        private void BuildObstacles()
        {
            foreach (var obstacle in MotorCourse.Obstacles)
            {
                var p = Point(obstacle.distance, MotorCourse.ObstacleLateral(obstacle, 0f));
                var root = Group(obstacle.kind == 2 ? "Moving road barrier" : obstacle.kind == 1 ? "Low trail log" : "Road rock", p, Quaternion.identity, transform);
                float diameter = obstacle.radius * 2f;
                if (obstacle.kind == 0)
                {
                    Antigravity("Props/09_Kaya_Engeli", p, Quaternion.Euler(0f, Noise(obstacle.distance, 2) * 360f, 0f),
                        Mathf.Max(0.72f, diameter / 1.58f), root);
                }
                else if (obstacle.kind == 1)
                {
                    Antigravity("Props/05_Duz_Uzun_Tahta", p, RoadRotation(obstacle.distance) * Quaternion.Euler(0f, 0f, 4f),
                        Mathf.Clamp(diameter / 3.4f, 0.55f, 0.90f), root);
                    Antigravity("Props/10_Ahsap_Euro_Palet", p + RoadRotation(obstacle.distance) * new Vector3(0f, 0f, 0.32f),
                        RoadRotation(obstacle.distance), 0.55f, root);
                }
                else
                {
                    Antigravity("Props/07_Ahsap_Yol_Barikati", p, RoadRotation(obstacle.distance),
                        Mathf.Clamp(diameter / 2.2f, 0.65f, 1.0f), root);
                    LocalBox("Barrier amber beacon", root, new Vector3(0f, 1.70f, 0f), new Vector3(0.23f, 0.20f, 0.23f), Mat("amber", new Color(1f, 0.67f, 0.15f)));
                    moving.Add(new MovingObstacle { obstacle = obstacle, root = root });
                    float w = MotorCourse.Sample(obstacle.distance).width;
                    Box("Barrier ground track", Point(obstacle.distance) + Vector3.up * 0.018f, new Vector3(w, 0.025f, 0.13f), Mat("wood", Wood));
                }
            }
        }

        private void BuildBasecamp(float distance, bool finish)
        {
            float centreD = finish ? distance + 6f : distance - 8f;
            var root = Group(finish ? "Summit tea house" : "Roadtrip service station", Point(centreD), Quaternion.identity);
            float side = finish ? 1f : -1f;
            LocalBox("Concrete forecourt", root, new Vector3(side * 11f, -0.09f, 0f), new Vector3(14f, 0.16f, 20f), Mat("forecourt", new Color(0.65f, 0.62f, 0.49f)));
            LocalBox("Building", root, new Vector3(side * 14f, 1.95f, 1f), new Vector3(6.5f, 3.9f, 7f), Mat("plaster", new Color(0.85f, 0.76f, 0.55f)));
            LocalBox("Terracotta roof", root, new Vector3(side * 14f, 4.02f, 1f), new Vector3(7.4f, 0.40f, 7.9f), Mat("orange", Orange));
            LocalBox("Shop sign", root, new Vector3(side * 14f, 3.11f, -2.58f), new Vector3(6.1f, 0.87f, 0.20f), Mat("teal", Teal));
            Text(finish ? "SON DURAK  /  ÇAY" : "YOL ARKADAŞI", root, new Vector3(side * 14f, 3.14f, -2.7f), 0.24f, Ivory);
            LocalBox("Door", root, new Vector3(side * 14f, 1.14f, -2.53f), new Vector3(1.35f, 2.3f, 0.09f), Mat("teal", Teal));
            for (int i = -1; i <= 1; i += 2)
            {
                LocalBox("Window frame", root, new Vector3(side * 14f + i * 2.15f, 1.64f, -2.56f), new Vector3(1.6f, 1.45f, 0.12f), Mat("ivory", Ivory));
                LocalBox("Window glass", root, new Vector3(side * 14f + i * 2.15f, 1.64f, -2.64f), new Vector3(1.34f, 1.18f, 0.045f), Mat("glass", new Color(0.26f, 0.44f, 0.43f), 0.35f));
            }
            LocalBox("Canopy", root, new Vector3(side * 8f, 3.65f, 0f), new Vector3(5.5f, 0.32f, 8f), Mat("teal", Teal));
            for (int i = -1; i <= 1; i += 2)
                LocalBox("Canopy post", root, new Vector3(side * 5.8f, 1.8f, i * 3.5f), new Vector3(0.18f, 3.6f, 0.18f), Mat("ivory", Ivory));
            if (!finish)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    LocalBox("Retro petrol pump", root, new Vector3(side * 8f, 0.91f, i * 1.8f), new Vector3(0.75f, 1.82f, 0.62f), Mat("orange", Orange));
                    LocalBox("Pump meter", root, new Vector3(side * 8f, 1.36f, i * 1.8f - 0.33f), new Vector3(0.61f, 0.39f, 0.045f), Mat("teal", Teal));
                    LocalBox("Pump base", root, new Vector3(side * 8f, 0.14f, i * 1.8f), new Vector3(1f, 0.28f, 0.8f), Mat("ivory", Ivory));
                }
                Text("ROTA 01  /  900 M", root, new Vector3(0f, 2.8f, 4f), 0.24f, Teal);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    LocalBox("Tea table", root, new Vector3(side * 8f, 0.8f, -2f + i * 2.4f), new Vector3(1.6f, 0.14f, 1.15f), Mat("woodLight", new Color(0.51f, 0.34f, 0.20f)));
                    LocalBox("Table leg", root, new Vector3(side * 8f, 0.4f, -2f + i * 2.4f), new Vector3(0.15f, 0.8f, 0.15f), Mat("teal", Teal));
                    LocalBox("Bench", root, new Vector3(side * 6.85f, 0.45f, -2f + i * 2.4f), new Vector3(0.55f, 0.90f, 1.3f), Mat("wood", Wood));
                }
            }
            float gateDistance = finish ? MotorCourse.Length : 8f;
            var banner = Group(finish ? "Finish line" : "Start line", Point(gateDistance), Quaternion.identity);
            float gateWidth = MotorCourse.Sample(gateDistance).width + 1.5f;
            for (int i = -1; i <= 1; i += 2)
                LocalBox("Journey gate post", banner, new Vector3(i * gateWidth * 0.5f, 2.6f, 0f), new Vector3(0.23f, 5.2f, 0.23f), Mat("teal", Teal));
            LocalBox("Journey banner", banner, new Vector3(0f, 4.8f, 0f), new Vector3(gateWidth + 0.3f, 0.78f, 0.22f), Mat("orange", Orange));
            Text(finish ? "BİRLİKTE BAŞARDINIZ" : "YOL ARKADAŞI", banner, new Vector3(0f, 4.82f, -0.13f), 0.25f, Ivory);
            if (finish)
            {
                for (int i = 0; i < 14; i++)
                    LocalBox("Finish chequer", banner, new Vector3(-gateWidth * 0.5f + (i + 0.5f) * gateWidth / 14f, 0.025f, 0f), new Vector3(gateWidth / 14f, 0.02f, 1f), i % 2 == 0 ? Mat("ivory", Ivory) : Mat("teal", Teal));
            }
            var campP = Point(finish ? 893f : 21f, finish ? -14f : 16f);
            Antigravity("Props/10_Ahsap_Euro_Palet", campP, Quaternion.Euler(0f, finish ? 22f : -35f, 0f), 1.0f);
            Antigravity("Props/01_Metal_Varil", campP + new Vector3(2.4f, 0f, -1f), Quaternion.identity, 0.9f);
            Antigravity("Props/02_Ahsap_Fici", campP + new Vector3(-2.2f, 0f, 1.2f), Quaternion.Euler(0f, 22f, 0f), 0.9f);
            AntigravityHeight(finish ? "Trees/05_Sakura_Agaci" : "Trees/01_Mese_Agaci",
                campP + new Vector3(finish ? -6f : 6f, 0f, 5f), finish ? 7.2f : 8.5f, finish ? 14f : -18f);
        }

        private void Decorate()
        {
            // Antigravity trees replace the old placeholder forest. Deterministic positions keep
            // host/client scenery identical without sharing RNG state.
            for (int i = 0; i < 118; i++)
            {
                float d = 28f + Noise(i * 9.41f, 7) * (MotorCourse.Length - 48f);
                if (d > 389f && d < 466f) continue;
                int side = i % 2 == 0 ? 1 : -1;
                float roadEdge = MotorCourse.Sample(d).width * 0.5f;
                float lateral = side * (roadEdge + 4.2f + Noise(i * 7.2f, 4) * 23f);
                var p = Point(d, lateral);
                p.y = TerrainHeight(d, lateral);
                float yaw = Noise(i * 13.3f, 2) * 360f;
                string id = i % 29 == 0 ? "Trees/05_Sakura_Agaci"
                    : i % 19 == 0 ? "Trees/06_Salkimsogut"
                    : i % 13 == 0 ? "Trees/01_Mese_Agaci"
                    : i % 9 == 0 ? "Trees/04_Hus_Agaci"
                    : i % 7 == 0 ? "Trees/07_Kuru_Agac"
                    : "Trees/02_Cam_Agaci";
                float height = id.Contains("Sakura") ? 6.2f + Noise(i, 8) * 1.6f : 5.0f + Noise(i, 8) * 5.0f;
                AntigravityHeight(id, p, height, yaw);
            }
            // Sparse low-poly landmarks make the wider free-roam terrain readable without
            // filling it with expensive meshes.
            for (int i = 0; i < 46; i++)
            {
                float d = 20f + Noise(i * 11.17f, 52) * (MotorCourse.Length - 20f);
                int side = i % 2 == 0 ? 1 : -1;
                float lateral = side * (36f + Noise(i * 8.3f, 47) * 31f);
                float worldX = MotorCourse.Sample(d).centerX + lateral;
                var p = new Vector3(worldX, MotorCourse.GroundHeight(d, worldX), d);
                string id = i % 8 == 0 ? "Trees/07_Kuru_Agac" : "Trees/02_Cam_Agaci";
                AntigravityHeight(id, p, 6f + Noise(i, 19) * 4f, Noise(i * 4.7f, 24) * 360f);
            }
            for (int i = 0; i < 42; i++)
            {
                float d = 44f + i * 20.1f;
                if (d > 390f && d < 465f) continue;
                int side = i % 2 == 0 ? -1 : 1;
                float lateral = side * (MotorCourse.Sample(d).width * 0.5f + 1.6f + Noise(i, 33) * 1.4f);
                var p = Point(d, lateral);
                p.y = TerrainHeight(d, lateral);
                string prop = i % 9 == 0 ? "Props/08_Lastik_Yigini" : i % 5 == 0 ? "Props/02_Ahsap_Fici" : "Props/03_Trafik_Konisi_Duba";
                Antigravity(prop, p, Quaternion.Euler(0f, Noise(i, 12) * 360f, 0f), prop.Contains("Konisi") ? 0.72f : 0.85f);
            }
        }

        private float TerrainHeight(float distance, float lateral)
        {
            var s = MotorCourse.Sample(distance);
            int side = lateral >= 0f ? 1 : -1;
            float edge = s.width * 0.5f + 0.70f;
            float t = Mathf.InverseLerp(edge, 26f, Mathf.Abs(lateral));
            float mid = s.height - 1.8f - CanyonDepth(distance) + Noise(distance, side) * 3.8f;
            return Mathf.Lerp(s.height - 0.08f - CanyonDepth(distance), mid, t);
        }

        private void BuildAntigravityMountainRange()
        {
            string[] models =
            {
                "Mountains/Desert_Mesa", "Mountains/Rolling_Highlands", "Mountains/Alpine_Mountain",
                "Mountains/Desert_Mesa", "Mountains/Volcano_Mountain", "Mountains/Rolling_Highlands",
                "Mountains/Alpine_Mountain", "Mountains/Desert_Mesa", "Mountains/Rolling_Highlands", "Mountains/Alpine_Mountain"
            };
            float[] distances = { -30f, 70f, 165f, 260f, 350f, 505f, 590f, 690f, 795f, 920f };
            for (int i = 0; i < models.Length; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                // Keep the central valley broad enough for free roam while leaving every mountain
                // inside the 480 m wide playable map and reachable from its gentler skirt.
                float lateral = side * (112f + Noise(i * 4.7f, 18) * 46f);
                var p = Point(distances[i], lateral);
                p.y = MotorCourse.Sample(distances[i]).height - 15f - Noise(i, 3) * 5f;
                // These GLBs were authored Z-up. Their FBX root arrives in Unity with the height
                // axis on local Z, so lay the terrain grid flat before measuring its target height.
                AntigravityHeight(models[i], p, 34f + Noise(i * 5.2f, 9) * 31f, 35f + i * 71f, -90f);
            }
        }

        private void ValidateAntigravityAssets()
        {
            string[] required =
            {
                "Mountains/Alpine_Mountain", "Mountains/Desert_Mesa", "Mountains/Rolling_Highlands", "Mountains/Volcano_Mountain",
                "Trees/01_Mese_Agaci", "Trees/02_Cam_Agaci", "Trees/03_Palmiye_Agaci", "Trees/04_Hus_Agaci",
                "Trees/05_Sakura_Agaci", "Trees/06_Salkimsogut", "Trees/07_Kuru_Agac",
                "Props/01_Metal_Varil", "Props/02_Ahsap_Fici", "Props/03_Trafik_Konisi_Duba", "Props/04_Silindir_Yol_Dubasi",
                "Props/05_Duz_Uzun_Tahta", "Props/06_Beton_Jersey_Bariyer", "Props/07_Ahsap_Yol_Barikati",
                "Props/08_Lastik_Yigini", "Props/09_Kaya_Engeli", "Props/10_Ahsap_Euro_Palet"
            };
            foreach (string path in required) LoadAntigravity(path);
            Debug.Log("ANTIGRAVITY_ASSET_CHECK_PASS " + required.Length + "/" + required.Length);
        }

        private GameObject LoadAntigravity(string resource)
        {
            string key = "Antigravity/" + resource;
            if (!nature.TryGetValue(key, out var prefab))
            {
                prefab = Resources.Load<GameObject>(key);
                nature.Add(key, prefab);
            }
            if (prefab == null) throw new InvalidOperationException("Missing Antigravity asset: " + key);
            return prefab;
        }

        private Transform Antigravity(string resource, Vector3 position, Quaternion rotation, float uniformScale = 1f, Transform parent = null)
        {
            var instance = Instantiate(LoadAntigravity(resource), position, rotation, parent != null ? parent : scenery);
            instance.name = "Antigravity · " + resource;
            instance.transform.localScale *= uniformScale;
            AlignToGround(instance.transform, position.y);
            RetintAntigravity(instance, resource);
            ConfigureAntigravityPhysics(instance, resource);
            AntigravityInstances++;
            return instance.transform;
        }

        private Transform AntigravityHeight(string resource, Vector3 position, float targetHeight, float yaw, float pitch = 0f)
        {
            var instance = Instantiate(LoadAntigravity(resource), position, Quaternion.Euler(pitch, yaw, 0f), scenery);
            instance.name = "Antigravity · " + resource;
            if (TryBounds(instance, out var bounds) && bounds.size.y > 0.001f)
                instance.transform.localScale *= targetHeight / bounds.size.y;
            AlignToGround(instance.transform, position.y);
            RetintAntigravity(instance, resource);
            ConfigureAntigravityPhysics(instance, resource);
            AntigravityInstances++;
            return instance.transform;
        }

        private void RetintAntigravity(GameObject instance, string resource)
        {
            if (resource.StartsWith("Mountains/", StringComparison.Ordinal))
            {
                Color fallback = resource.Contains("Desert") ? new Color(.76f, .34f, .19f)
                    : resource.Contains("Rolling") ? new Color(.32f, .52f, .18f)
                    : resource.Contains("Volcano") ? new Color(.18f, .17f, .19f)
                    : new Color(.42f, .45f, .49f);
                bool hasVertexColors = false;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    if (mesh != null && mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount)
                    {
                        hasVertexColors = true;
                        break;
                    }
                }
                Material mountain = AntigravityMaterial("mountain-" + resource, fallback, hasVertexColors);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = mountain;
                    renderer.sharedMaterials = slots;
                }
                return;
            }

            if (!resource.StartsWith("Trees/", StringComparison.Ordinal)) return;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string source = slots[i] != null ? slots[i].name.ToLowerInvariant() : string.Empty;
                    Color color = TreePartColor(resource, source, i, slots.Length);
                    slots[i] = Mat("antigravity-tree-" + resource + "-" + i + "-" + ColorUtility.ToHtmlStringRGB(color), color, .05f);
                }
                renderer.sharedMaterials = slots;
            }
        }

        private Material AntigravityMaterial(string key, Color fallback, bool useVertexColors)
        {
            if (materials.TryGetValue(key, out var existing)) return existing;
            var shader = Shader.Find("DortCuce/AntigravityVertexColor") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Antigravity terrain · " + key, color = fallback };
            if (material.HasProperty("_Color")) material.SetColor("_Color", fallback);
            if (material.HasProperty("_UseVertexColor")) material.SetFloat("_UseVertexColor", useVertexColors ? 1f : 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .08f);
            material.enableInstancing = true;
            materials.Add(key, material);
            return material;
        }

        private static Color TreePartColor(string resource, string materialName, int index, int count)
        {
            bool foliage = materialName.Contains("leaf") || materialName.Contains("needle") || materialName.Contains("frond")
                || materialName.Contains("blossom");
            bool bark = materialName.Contains("bark") || materialName.Contains("trunk") || materialName.Contains("wood")
                || materialName.Contains("coconut");
            if (!foliage && !bark && count > 1)
            {
                if (resource.Contains("Palmiye")) foliage = index == 1;
                else foliage = index == count - 1;
            }
            if (resource.Contains("Kuru")) return new Color(.31f, .19f, .10f);
            if (resource.Contains("Sakura") && foliage) return new Color(.95f, .47f, .64f);
            if (resource.Contains("Hus") && !foliage) return new Color(.72f, .69f, .58f);
            if (foliage)
                return resource.Contains("Cam") ? new Color(.12f, .32f, .16f) : new Color(.24f, .48f, .18f);
            return resource.Contains("Palmiye") ? new Color(.44f, .28f, .12f) : new Color(.34f, .20f, .10f);
        }

        private void ConfigureAntigravityPhysics(GameObject instance, string resource)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
            if (resource.StartsWith("Mountains/", StringComparison.Ordinal))
            {
                SetLayerRecursive(instance.transform, MotorCourse.MountainLayer);
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    MountainColliders++;
                }
            }
            else if (resource.StartsWith("Trees/", StringComparison.Ordinal) && TryBounds(instance, out var bounds))
            {
                var trunk = new GameObject("Physical trunk · " + resource);
                trunk.layer = MotorCourse.TreeLayer;
                trunk.transform.SetParent(scenery, false);
                trunk.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                float typeScale = resource.Contains("Mese") || resource.Contains("Salkim") ? .052f : .036f;
                float radius = Mathf.Clamp(bounds.size.y * typeScale, .20f, .62f);
                float height = Mathf.Clamp(bounds.size.y * .58f, 1.8f, 5.2f);
                var capsule = trunk.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = Mathf.Max(height, radius * 2f);
                capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
                TreeColliders++;
            }
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursive(root.GetChild(i), layer);
        }

        private static void AlignToGround(Transform instance, float groundY)
        {
            if (TryBounds(instance.gameObject, out var bounds))
                instance.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static bool TryBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { bounds = default; return false; }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private void Sign(float distance, int side, string title, string subtitle, bool warning)
        {
            var sample = MotorCourse.Sample(distance);
            var root = Group("Route sign · " + title, Point(distance, side * (sample.width * 0.5f + 2.6f)), RoadRotation(distance));
            LocalBox("Sign post", root, new Vector3(0f, 1.35f, 0f), new Vector3(0.13f, 2.7f, 0.13f), Mat("wood", Wood));
            LocalBox("Route sign board", root, new Vector3(0f, 2.54f, 0f), new Vector3(4.25f, 1.24f, 0.14f), warning ? Mat("orange", Orange) : Mat("teal", Teal));
            Text(title, root, new Vector3(0f, 2.76f, -0.084f), 0.21f, Ivory);
            Text(subtitle, root, new Vector3(0f, 2.29f, -0.084f), 0.105f, Ivory);
        }

        private void Text(string value, Transform parent, Vector3 localPosition, float size, Color color)
        {
            var text = new GameObject(value).AddComponent<TextMesh>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = localPosition;
            text.text = value;
            text.font = font;
            text.fontSize = 64;
            // TextMesh multiplies characterSize by the chosen fontSize. Treat callers' size as
            // an actual world-space text height so high-resolution glyphs do not become giant signs.
            text.characterSize = size * 10f / text.fontSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            // All boards place lettering on their local -Z face, toward approaching riders.
            text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private Material Mat(string key, Color color, float smoothness = 0.02f)
        {
            if (materials.TryGetValue(key, out var existing)) return existing;
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "Roadtrip · " + key, color = color };
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            if (key == "road") material.mainTexture = BuildAsphaltTexture();
            materials.Add(key, material);
            return material;
        }

        private Texture2D BuildAsphaltTexture()
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGB24, false) { name = "Antigravity procedural asphalt" };
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float fine = Noise(x * 1.7f + y * 0.31f, 41);
                    float coarse = Noise(x * 0.13f + y * 0.27f, 11);
                    float value = 0.72f + (fine - 0.5f) * 0.20f + (coarse - 0.5f) * 0.10f;
                    pixels[y * 128 + x] = new Color(value, value * 0.98f, value * 0.94f);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            ownedTextures.Add(texture);
            return texture;
        }

        private Transform Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : scenery, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        private Transform LocalBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var result = Box(name, Vector3.zero, scale, material, parent);
            result.localPosition = position;
            result.localRotation = Quaternion.identity;
            return result;
        }

        private Transform Group(string name, Vector3 position, Quaternion rotation, Transform parent = null, bool local = false)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent != null ? parent : scenery, false);
            if (local) { result.localPosition = position; result.localRotation = rotation; }
            else { result.position = position; result.rotation = rotation; }
            return result;
        }

        private void Beam(string name, Vector3 start, Vector3 end, float thickness, Material material)
        {
            var beam = Box(name, (start + end) * 0.5f, new Vector3(thickness, thickness, Vector3.Distance(start, end)), material);
            beam.rotation = Quaternion.LookRotation(end - start, Vector3.up);
        }

        private void Mountain(string name, Vector3 bottom, float radius, float height, Material material, int sides, Transform parent = null)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 baseA = bottom + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Vector3 baseB = bottom + new Vector3(Mathf.Cos(b) * radius, 0f, Mathf.Sin(b) * radius);
                Vector3 shoulderA = bottom + new Vector3(Mathf.Cos(a) * radius * 0.65f, height * 0.53f, Mathf.Sin(a) * radius * 0.65f);
                Vector3 shoulderB = bottom + new Vector3(Mathf.Cos(b) * radius * 0.65f, height * 0.53f, Mathf.Sin(b) * radius * 0.65f);
                AddQuad(vertices, triangles, baseB, baseA, shoulderA, shoulderB);
                int n = vertices.Count;
                vertices.Add(shoulderB); vertices.Add(shoulderA); vertices.Add(bottom + new Vector3(radius * 0.12f, height, radius * 0.06f));
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            }
            MeshObject(name, vertices, triangles, material, parent);
        }

        private void MeshObject(string name, List<Vector3> vertices, List<int> triangles, Material material, Transform parent = null)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var uvs = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++) uvs.Add(new Vector2(vertices[i].x * 0.18f, vertices[i].z * 0.18f));
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent != null ? parent : scenery, true);
            // Vertices above are authored in world space, including when grouped under a moving obstacle.
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
            triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2);
        }

        private static void AddQuadUp(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (Vector3.Cross(c - a, b - a).y >= 0f) AddQuad(vertices, triangles, a, b, c, d);
            else AddQuad(vertices, triangles, d, c, b, a);
        }

        private static Quaternion RoadRotation(float distance)
        {
            return Quaternion.LookRotation((Point(distance + 0.3f) - Point(distance - 0.3f)).normalized, Vector3.up);
        }

        private static float Noise(float value, int seed)
        {
            return Mathf.PerlinNoise(value * 0.1731f + seed * 18.73f, seed * 6.71f + 21.97f);
        }

        private void OnDestroy()
        {
            MotorCourse.EnvironmentPhysicsReady = false;
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            foreach (var texture in ownedTextures) if (texture != null) Destroy(texture);
        }
    }
}
