using System.Collections.Generic;
using UnityEngine;

namespace DortCuce.UnityGame
{
    public sealed class DortCuceGame : MonoBehaviour
    {
        private static readonly Color RoadColor = new(0.20f, 0.18f, 0.16f);
        private static readonly Color EdgeColor = new(0.76f, 0.46f, 0.17f);
        private static readonly Color EarthColor = new(0.34f, 0.23f, 0.12f);
        private static readonly Color GrassColor = new(0.18f, 0.37f, 0.18f);
        private readonly List<Material> materials = new();
        private TrackNode[] track = null!;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            track = DortCuceTrackDefinition.Create();
            DortCuceTrackDefinition.Validate(track);
            BuildEnvironment();
            BuildTrack();
            BuildBike();
        }

        private void BuildEnvironment()
        {
            RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.48f, 0.63f, 0.66f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 360f;

            var sun = new GameObject("Warm Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.86f, 0.66f);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var canyon = Primitive("Canyon Floor", PrimitiveType.Cube, new Vector3(0f, -8f, 390f),
                new Vector3(150f, 12f, 820f), EarthColor, true);
            canyon.transform.SetParent(transform);

            for (var i = 0; i < 150; i++)
            {
                var z = i * 5.3f + 3f;
                var side = i % 2 == 0 ? -1f : 1f;
                var x = side * (24f + Mathf.PerlinNoise(i * 0.31f, 1.7f) * 38f);
                var y = -0.8f + Mathf.PerlinNoise(i * 0.17f, 4.1f) * 6f;

                if (i % 3 == 0)
                {
                    CreateTree(new Vector3(x, y, z), 0.8f + Mathf.PerlinNoise(i, 8f) * 1.2f);
                }
                else
                {
                    var rock = Primitive("Canyon Rock", PrimitiveType.Sphere, new Vector3(x, y, z),
                        new Vector3(2.5f, 1.8f, 2.2f) * (0.7f + Mathf.PerlinNoise(i, 3f)),
                        i % 4 == 0 ? EdgeColor : EarthColor, true);
                    rock.transform.rotation = Quaternion.Euler(i * 17f, i * 29f, i * 11f);
                    rock.transform.SetParent(transform);
                }
            }
        }

        private void BuildTrack()
        {
            for (var i = 0; i < track.Length - 1; i++)
            {
                var current = track[i];
                var next = track[i + 1];
                var delta = next.Position - current.Position;
                var midpoint = (current.Position + next.Position) * 0.5f;
                var width = Mathf.Min(current.Width, next.Width);
                var road = Primitive($"Road {i:00}", PrimitiveType.Cube, midpoint - Vector3.up * 0.24f,
                    new Vector3(width, 0.48f, delta.magnitude + 0.35f), RoadColor, true);
                road.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                road.transform.SetParent(transform);

                if (width < 4f)
                {
                    CreateEdgePost(current.Position, delta, -width * 0.5f - 0.25f, i);
                    CreateEdgePost(current.Position, delta, width * 0.5f + 0.25f, i);
                }

                if (i is 8 or 29 or 58)
                {
                    var bump = Primitive("Balance Bump", PrimitiveType.Cube,
                        current.Position + Vector3.up * 0.18f,
                        new Vector3(width * 0.88f, 0.28f, 0.8f), EdgeColor, true);
                    bump.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    bump.transform.SetParent(transform);
                }

                if (i > 0 && i % 10 == 0)
                {
                    CreateCheckpoint(current.Position, delta.normalized, width, i / 10);
                }
            }

            CreateFinish(track[^1].Position, (track[^1].Position - track[^2].Position).normalized);
        }

        private void CreateEdgePost(Vector3 center, Vector3 direction, float sideOffset, int index)
        {
            var right = Vector3.Cross(Vector3.up, direction.normalized);
            var position = center + right * sideOffset + Vector3.up * 0.45f;
            var post = Primitive($"Narrow Edge {index}", PrimitiveType.Cylinder, position,
                new Vector3(0.16f, 0.65f, 0.16f), index % 2 == 0 ? Color.white : EdgeColor, true);
            post.transform.SetParent(transform);
        }

        private void CreateCheckpoint(Vector3 center, Vector3 direction, float width, int number)
        {
            var right = Vector3.Cross(Vector3.up, direction);
            var gate = new GameObject($"Checkpoint {number}");
            gate.transform.SetParent(transform);
            gate.transform.position = center;
            gate.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            CreateChildPrimitive(gate.transform, "Left Flag", PrimitiveType.Cube,
                new Vector3(-width * 0.5f - 0.35f, 1.3f, 0f), new Vector3(0.18f, 2.6f, 0.18f), EdgeColor);
            CreateChildPrimitive(gate.transform, "Right Flag", PrimitiveType.Cube,
                new Vector3(width * 0.5f + 0.35f, 1.3f, 0f), new Vector3(0.18f, 2.6f, 0.18f), EdgeColor);
            CreateChildPrimitive(gate.transform, "Banner", PrimitiveType.Cube,
                new Vector3(0f, 2.55f, 0f), new Vector3(width + 0.9f, 0.3f, 0.18f), Color.white);
        }

        private void CreateFinish(Vector3 center, Vector3 direction)
        {
            var finish = new GameObject("Tea House Finish");
            finish.transform.SetParent(transform);
            finish.transform.position = center + direction * 5f;
            finish.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            CreateChildPrimitive(finish.transform, "Tea House", PrimitiveType.Cube,
                new Vector3(7f, 2f, 5f), new Vector3(6f, 4f, 5f), new Color(0.56f, 0.25f, 0.13f));
            CreateChildPrimitive(finish.transform, "Roof", PrimitiveType.Cube,
                new Vector3(7f, 4.2f, 5f), new Vector3(7f, 0.5f, 6f), EdgeColor);
        }

        private void CreateTree(Vector3 position, float scale)
        {
            var root = new GameObject("Procedural Tree");
            root.transform.SetParent(transform);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            CreateChildPrimitive(root.transform, "Trunk", PrimitiveType.Cylinder,
                new Vector3(0f, 1.2f, 0f), new Vector3(0.34f, 1.2f, 0.34f), new Color(0.27f, 0.16f, 0.08f));
            CreateChildPrimitive(root.transform, "Leaves", PrimitiveType.Sphere,
                new Vector3(0f, 3f, 0f), new Vector3(2.2f, 2.4f, 2.2f), GrassColor);
        }

        private void BuildBike()
        {
            var bike = new GameObject("Four Gnomes Motorcycle");
            bike.layer = 2;
            bike.transform.position = track[0].Position + Vector3.up * 1.15f;
            bike.transform.rotation = Quaternion.LookRotation((track[1].Position - track[0].Position).normalized, Vector3.up);

            var bodyCollider = bike.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, 0.55f, 0f);
            bodyCollider.size = new Vector3(0.9f, 0.8f, 2.3f);

            var rb = bike.AddComponent<Rigidbody>();
            rb.mass = 230f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 1.1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0f, -0.42f, 0.05f);

            var controller = bike.AddComponent<DortCuceBikeController>();
            controller.Configure(track);

            CreateBikeVisuals(bike.transform, controller);

            var cameraObject = new GameObject("Follow Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 66f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 520f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<DortCuceFollowCamera>().Target = bike.transform;
        }

        private void CreateBikeVisuals(Transform bike, DortCuceBikeController controller)
        {
            var frame = CreateChildPrimitive(bike, "Motorcycle Frame", PrimitiveType.Cube,
                new Vector3(0f, 0.48f, 0f), new Vector3(0.42f, 0.34f, 1.8f), new Color(0.70f, 0.12f, 0.06f));
            var tank = CreateChildPrimitive(bike, "Fuel Tank", PrimitiveType.Sphere,
                new Vector3(0f, 0.82f, 0.25f), new Vector3(0.76f, 0.62f, 0.88f), EdgeColor);
            var seat = CreateChildPrimitive(bike, "Long Seat", PrimitiveType.Cube,
                new Vector3(0f, 0.9f, -0.45f), new Vector3(0.72f, 0.22f, 1.25f), new Color(0.07f, 0.06f, 0.06f));
            var frontWheel = CreateWheel(bike, "Front Wheel", new Vector3(0f, 0.36f, 1.12f));
            var rearWheel = CreateWheel(bike, "Rear Wheel", new Vector3(0f, 0.36f, -1.12f));
            controller.FrontWheel = frontWheel;
            controller.RearWheel = rearWheel;

            var fork = CreateChildPrimitive(bike, "Front Fork", PrimitiveType.Cube,
                new Vector3(0f, 0.78f, 0.92f), new Vector3(0.16f, 1.05f, 0.16f), new Color(0.18f, 0.18f, 0.18f));
            fork.transform.localRotation = Quaternion.Euler(16f, 0f, 0f);
            var handlebar = CreateChildPrimitive(bike, "Handlebar", PrimitiveType.Cylinder,
                new Vector3(0f, 1.28f, 0.78f), new Vector3(0.08f, 0.62f, 0.08f), new Color(0.1f, 0.1f, 0.1f));
            handlebar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            controller.Handlebar = handlebar.transform;

            controller.Gnomes = new[]
            {
                CreateGnome(bike, "Gogo - Gas", new Vector3(-0.25f, 1.18f, 0.34f), new Color(0.93f, 0.39f, 0.08f), GnomeRole.Throttle),
                CreateGnome(bike, "Brik - Brake", new Vector3(0.25f, 1.18f, 0.62f), new Color(0.15f, 0.55f, 0.92f), GnomeRole.Brake),
                CreateGnome(bike, "Misket - Gear", new Vector3(-0.24f, 1.18f, -0.35f), new Color(0.18f, 0.72f, 0.35f), GnomeRole.Gear),
                CreateGnome(bike, "Piko - Balance", new Vector3(0.24f, 1.18f, -0.62f), new Color(0.64f, 0.28f, 0.82f), GnomeRole.Balance),
            };

            controller.Frame = frame.transform;
            controller.Tank = tank.transform;
            controller.Seat = seat.transform;
        }

        private Transform CreateWheel(Transform parent, string name, Vector3 localPosition)
        {
            var wheel = CreateChildPrimitive(parent, name, PrimitiveType.Cylinder, localPosition,
                new Vector3(0.62f, 0.16f, 0.62f), new Color(0.035f, 0.035f, 0.035f));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return wheel.transform;
        }

        private GnomeRig CreateGnome(Transform parent, string name, Vector3 localPosition, Color color, GnomeRole role)
        {
            var root = new GameObject(name);
            root.layer = 2;
            root.transform.SetParent(parent);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * 0.48f;

            var body = CreateChildPrimitive(root.transform, "Body", PrimitiveType.Capsule,
                Vector3.zero, new Vector3(0.52f, 0.66f, 0.44f), color);
            var head = CreateChildPrimitive(root.transform, "Head", PrimitiveType.Sphere,
                new Vector3(0f, 0.88f, 0f), Vector3.one * 0.55f, new Color(0.92f, 0.70f, 0.52f));
            var hat = CreateChildPrimitive(root.transform, "Hat", PrimitiveType.Cylinder,
                new Vector3(0f, 1.25f, 0f), new Vector3(0.44f, 0.48f, 0.44f), color * 0.78f);
            var leftArm = CreateChildPrimitive(root.transform, "Left Arm", PrimitiveType.Capsule,
                new Vector3(-0.42f, 0.34f, 0.1f), new Vector3(0.19f, 0.48f, 0.19f), new Color(0.92f, 0.70f, 0.52f));
            var rightArm = CreateChildPrimitive(root.transform, "Right Arm", PrimitiveType.Capsule,
                new Vector3(0.42f, 0.34f, 0.1f), new Vector3(0.19f, 0.48f, 0.19f), new Color(0.92f, 0.70f, 0.52f));
            var leftLeg = CreateChildPrimitive(root.transform, "Left Leg", PrimitiveType.Capsule,
                new Vector3(-0.2f, -0.57f, 0f), new Vector3(0.2f, 0.38f, 0.2f), new Color(0.12f, 0.11f, 0.10f));
            var rightLeg = CreateChildPrimitive(root.transform, "Right Leg", PrimitiveType.Capsule,
                new Vector3(0.2f, -0.57f, 0f), new Vector3(0.2f, 0.38f, 0.2f), new Color(0.12f, 0.11f, 0.10f));

            Transform pole = null;
            if (role == GnomeRole.Balance)
            {
                var poleObject = CreateChildPrimitive(root.transform, "Balance Pole", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.42f, 0f), new Vector3(0.11f, 2.25f, 0.11f), new Color(0.37f, 0.21f, 0.09f));
                poleObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pole = poleObject.transform;
            }

            return new GnomeRig(root.transform, body.transform, head.transform, hat.transform,
                leftArm.transform, rightArm.transform, leftLeg.transform, rightLeg.transform, pole, role);
        }

        private GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            var primitiveCollider = go.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = collider;
            }
            return go;
        }

        private GameObject CreateChildPrimitive(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 scale, Color color)
        {
            var go = Primitive(name, type, Vector3.zero, scale, color, false);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private Material Material(Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color };
            materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }
    }

    public enum GnomeRole
    {
        Throttle,
        Brake,
        Gear,
        Balance,
    }

    public sealed class GnomeRig
    {
        public GnomeRig(Transform root, Transform body, Transform head, Transform hat, Transform leftArm,
            Transform rightArm, Transform leftLeg, Transform rightLeg, Transform pole, GnomeRole role)
        {
            Root = root;
            Body = body;
            Head = head;
            Hat = hat;
            LeftArm = leftArm;
            RightArm = rightArm;
            LeftLeg = leftLeg;
            RightLeg = rightLeg;
            Pole = pole;
            Role = role;
        }

        public Transform Root { get; }
        public Transform Body { get; }
        public Transform Head { get; }
        public Transform Hat { get; }
        public Transform LeftArm { get; }
        public Transform RightArm { get; }
        public Transform LeftLeg { get; }
        public Transform RightLeg { get; }
        public Transform Pole { get; }
        public GnomeRole Role { get; }
    }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class DortCuceBikeController : MonoBehaviour
    {
        private Rigidbody rb = null!;
        private TrackNode[] track = null!;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float throttle;
        private float braking;
        private float steering;
        private float balance;
        private float shiftAnimation;
        private int gear = 1;
        private bool finished;
        private GUIStyle titleStyle = null!;
        private GUIStyle bodyStyle = null!;
        private GUIStyle warningStyle = null!;

        public Transform FrontWheel { get; set; } = null!;
        public Transform RearWheel { get; set; } = null!;
        public Transform Handlebar { get; set; } = null!;
        public Transform Frame { get; set; } = null!;
        public Transform Tank { get; set; } = null!;
        public Transform Seat { get; set; } = null!;
        public GnomeRig[] Gnomes { get; set; } = System.Array.Empty<GnomeRig>();

        public float SpeedKph => rb == null ? 0f : rb.linearVelocity.magnitude * 3.6f;
        public float LeanAngle => Vector3.SignedAngle(transform.up, Vector3.up, transform.forward);

        public void Configure(TrackNode[] trackNodes)
        {
            track = trackNodes;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            throttle = Input.GetKey(KeyCode.W) ? 1f : 0f;
            braking = Input.GetKey(KeyCode.S) ? 1f : 0f;
            steering = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            balance = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);

            if (Input.GetKeyDown(KeyCode.E))
            {
                gear = Mathf.Min(5, gear + 1);
                shiftAnimation = 1f;
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                gear = Mathf.Max(1, gear - 1);
                shiftAnimation = -1f;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetBike();
            }

            AnimateBike();
            finished = finished || Progress > 0.985f;
        }

        private void FixedUpdate()
        {
            if (track == null || track.Length < 2)
            {
                return;
            }

            var grounded = Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out _, 1.55f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            var speed = Mathf.Abs(forwardSpeed);
            var engineForce = (1850f + gear * 420f) * throttle;
            var brakeForce = 2850f * braking;

            if (grounded && !finished)
            {
                rb.AddForce(transform.forward * engineForce, ForceMode.Force);
                if (speed > 0.2f)
                {
                    rb.AddForce(-rb.linearVelocity.normalized * brakeForce, ForceMode.Force);
                }

                var steeringAuthority = Mathf.Lerp(145f, 390f, Mathf.Clamp01(speed / 18f));
                rb.AddTorque(Vector3.up * steering * steeringAuthority, ForceMode.Force);

                var rollRate = Vector3.Dot(rb.angularVelocity, transform.forward);
                var weakSelfRighting = -LeanAngle * 3.2f - rollRate * 72f;
                var poleAuthority = balance * (680f + speed * 28f);
                var roadChaos = Mathf.Sin(Time.time * 1.7f + transform.position.z * 0.09f) * (35f + speed * 2.2f);
                var steeringLean = -steering * speed * 19f;
                rb.AddTorque(transform.forward * (weakSelfRighting + poleAuthority + roadChaos + steeringLean), ForceMode.Force);
                rb.AddForce(-transform.up * Mathf.Lerp(300f, 1250f, Mathf.Clamp01(speed / 22f)), ForceMode.Force);
            }

            var localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            localVelocity.x *= 0.94f;
            rb.linearVelocity = transform.TransformDirection(localVelocity);

            var maximumSpeed = 15f + gear * 5f;
            if (rb.linearVelocity.magnitude > maximumSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maximumSpeed;
            }

            if (transform.position.y < -11f || Mathf.Abs(LeanAngle) > 82f)
            {
                ResetBike();
            }
        }

        private float Progress
        {
            get
            {
                if (track == null || track.Length == 0)
                {
                    return 0f;
                }
                return Mathf.InverseLerp(track[0].Position.z, track[^1].Position.z, transform.position.z);
            }
        }

        private void AnimateBike()
        {
            var speed = SpeedKph;
            var spin = speed * Time.deltaTime * 6.5f;
            if (FrontWheel != null) FrontWheel.Rotate(Vector3.up, spin, Space.Self);
            if (RearWheel != null) RearWheel.Rotate(Vector3.up, spin, Space.Self);
            if (Handlebar != null) Handlebar.localRotation = Quaternion.Euler(0f, steering * 24f, 90f);

            shiftAnimation = Mathf.MoveTowards(shiftAnimation, 0f, Time.deltaTime * 4f);
            var bounce = Mathf.Sin(Time.time * (5f + speed * 0.08f)) * Mathf.Clamp01(speed / 35f);

            foreach (var gnome in Gnomes)
            {
                var rolePower = gnome.Role switch
                {
                    GnomeRole.Throttle => throttle,
                    GnomeRole.Brake => braking,
                    GnomeRole.Gear => Mathf.Abs(shiftAnimation),
                    GnomeRole.Balance => Mathf.Abs(balance),
                    _ => 0f,
                };

                var roleDirection = gnome.Role == GnomeRole.Gear ? Mathf.Sign(shiftAnimation) : balance;
                gnome.Root.localRotation = Quaternion.Euler(bounce * 4f + rolePower * 7f, 0f,
                    gnome.Role == GnomeRole.Balance ? -balance * 18f : steering * 3f);
                gnome.Head.localRotation = Quaternion.Euler(-bounce * 5f, roleDirection * 7f, 0f);
                gnome.Hat.localRotation = Quaternion.Euler(bounce * 8f, 0f, -roleDirection * 4f);
                gnome.LeftArm.localRotation = Quaternion.Euler(20f + rolePower * 55f, 0f, 12f);
                gnome.RightArm.localRotation = Quaternion.Euler(20f - rolePower * 55f, 0f, -12f);
                gnome.LeftLeg.localRotation = Quaternion.Euler(bounce * 10f, 0f, 0f);
                gnome.RightLeg.localRotation = Quaternion.Euler(-bounce * 10f, 0f, 0f);
                if (gnome.Pole != null)
                {
                    gnome.Pole.localRotation = Quaternion.Euler(0f, 0f, 90f - balance * 24f);
                }
            }
        }

        private void ResetBike()
        {
            var index = track == null ? 0 : Mathf.Clamp(Mathf.FloorToInt(Progress * (track.Length - 1)) - 2, 0, track.Length - 2);
            var position = track == null ? spawnPosition : track[index].Position + Vector3.up * 1.35f;
            var rotation = track == null ? spawnRotation : Quaternion.LookRotation((track[index + 1].Position - track[index].Position).normalized, Vector3.up);
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
            warningStyle ??= new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.63f, 0.16f) } };

            GUI.Box(new Rect(18, 18, 390, 174), string.Empty);
            GUI.Label(new Rect(34, 28, 420, 35), "FOUR RIDERS, ONE MOTORCYCLE · UNITY", titleStyle);
            GUI.Label(new Rect(34, 68, 360, 110),
                $"W/S Throttle–Brake   A/D Steering\n←/→ Balance pole   Q/E Gears   R Recover\nSpeed: {SpeedKph:0} km/h   Gear: {gear}   Lean: {LeanAngle:0}°\nCourse: {Progress * 100f:0}%", bodyStyle);

            if (Mathf.Abs(LeanAngle) > 42f)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 240f, 30f, 480f, 45f), "SHIFT YOUR WEIGHT THE OTHER WAY!", warningStyle);
            }
            if (finished)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - 80f, 520f, 160f), string.Empty);
                GUI.Label(new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.5f - 45f, 460f, 90f),
                    "YOU REACHED THE TEA STOP!\nPress R to ride the course again.", warningStyle);
            }
        }
    }

    public sealed class DortCuceFollowCamera : MonoBehaviour
    {
        public Transform Target { get; set; } = null!;
        private Vector3 velocity;

        private void LateUpdate()
        {
            if (Target == null) return;
            var desired = Target.position - Target.forward * 7.8f + Vector3.up * 4.4f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.16f);
            var lookTarget = Target.position + Target.forward * 4.5f + Vector3.up * 0.85f;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookTarget - transform.position, Vector3.up), Time.deltaTime * 7f);
        }
    }
}
