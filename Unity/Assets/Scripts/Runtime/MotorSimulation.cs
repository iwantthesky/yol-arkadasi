using System;
using UnityEngine;

namespace DortCuce.UnityGame
{
    [Serializable]
    public struct MotorInput
    {
        public float throttle, brake, steer, balance, clutch;
        public int shift;
        public bool ignition, reset;
    }

    [Serializable]
    public class BikeSnapshot
    {
        public float distance, lateral, elevation, verticalSpeed, speed, lean, leanVelocity, heading, steeringAngle, rpm, elapsed;
        public int gear, checkpoint, crashes;
        public bool engineRunning, crashed, finished, grounded;
        public string message;
    }

    public struct CourseSample
    {
        public float centerX, height, width, bank, wind;
    }

    public struct CourseObstacle
    {
        public float distance, lateral, radius;
        public int kind;

        public CourseObstacle(float distance, float lateral, float radius, int kind)
        {
            this.distance = distance;
            this.lateral = lateral;
            this.radius = radius;
            this.kind = kind;
        }
    }

    /// <summary>Shared course geometry for collision, scenery and network clients. Distances are metres.</summary>
    public static class MotorCourse
    {
        public const float Length = 900f;
        public const float MapMinZ = -180f;
        public const float MapMaxZ = 1080f;
        public const float RidgeWidth = 72f;
        public const float MapHalfWidth = 240f;
        public const int MountainLayer = 2;
        public const int TreeLayer = 3;
        public const float MaximumClimbGrade = .67f;
        public static bool EnvironmentPhysicsReady { get; set; }
        public static readonly float[] Checkpoints = { 0f, 180f, 360f, 540f, 720f };
        public static readonly CourseObstacle[] Obstacles =
        {
            new CourseObstacle(112f, -2.5f, .80f, 0),
            new CourseObstacle(152f, 2.5f, .80f, 0),
            new CourseObstacle(209f, -2.0f, .85f, 0),
            new CourseObstacle(239f, 1.9f, .85f, 0),
            new CourseObstacle(269f, -1.6f, 1.00f, 1),
            new CourseObstacle(310f, 2.15f, .65f, 0),
            new CourseObstacle(514f, -2.45f, .70f, 0),
            new CourseObstacle(565f, 1.65f, 1.05f, 1),
            new CourseObstacle(657f, 0f, .85f, 2),
            new CourseObstacle(763f, -2.2f, .85f, 0),
            new CourseObstacle(796f, 2.3f, .75f, 0),
            new CourseObstacle(852f, 0f, .80f, 2)
        };

        public static CourseSample Sample(float distance)
        {
            float d = Mathf.Clamp(distance, 0f, Length);
            float intro = Smooth(55f, 125f, d);
            float windZone = Window(d, 296f, 320f, 379f, 397f);
            float bridge = Window(d, 380f, 400f, 455f, 474f);
            float height = .45f + intro * (.35f * Mathf.Sin(d * .013f) + .18f * Mathf.Sin(d * .031f));
            height += Ramp(d, 476f, 490f, 501f, 2.65f);
            height += Ramp(d, 603f, 620f, 634f, 3.40f);
            height += 9f * Smooth(680f, 780f, d) - 4f * Smooth(795f, 870f, d);

            return new CourseSample
            {
                centerX = intro * (3.3f * Mathf.Sin(d * .010f) + 1.8f * Mathf.Sin(d * .024f)),
                height = height,
                width = Mathf.Lerp(10f - 1.2f * Smooth(130f, 160f, d), 3f, bridge) - windZone * 1.0f,
                bank = .025f * Mathf.Sin(d * .024f) * intro * (1f - bridge),
                wind = windZone * (.85f + .25f * Mathf.Sin(d * .073f))
            };
        }

        public static float ObstacleLateral(CourseObstacle obstacle, float elapsed)
        {
            return obstacle.kind == 2
                ? obstacle.lateral + Mathf.Sin(elapsed * .88f + obstacle.distance * .016f) * 2.75f
                : obstacle.lateral;
        }

        public static float CourseHeading(float distance)
        {
            const float sample = .5f;
            return Mathf.Atan2(Sample(distance + sample).centerX - Sample(distance - sample).centerX, sample * 2f);
        }

        public static bool IsOnRoad(float distance, float lateral)
        {
            return Mathf.Abs(lateral) <= Sample(distance).width * .5f + .08f;
        }

        /// <summary>Height of the rendered road/terrain at a world-space X/Z point.</summary>
        public static float GroundHeight(float distance, float worldX)
        {
            float baseHeight = BaseGroundHeight(distance, worldX);
            if (TryGetMountainSurface(distance, worldX, out var hit) && hit.point.y > baseHeight + .03f)
                return hit.point.y;
            return baseHeight;
        }

        public static float BaseGroundHeight(float distance, float worldX)
        {
            CourseSample surface = Sample(distance);
            float lateral = worldX - surface.centerX;
            float absolute = Mathf.Abs(lateral);
            if (absolute <= surface.width * .5f + .08f) return surface.height;

            int side = lateral >= 0f ? 1 : -1;
            float gap = CanyonDepth(distance);
            float edge = surface.width * .5f + .70f;
            float near = surface.height - .08f - gap;
            float mid = surface.height - 1.8f - gap + Noise(distance, side) * 3.8f;
            if (absolute <= 26f) return Mathf.Lerp(near, mid, Mathf.InverseLerp(edge, 26f, absolute));
            float far = surface.height - 8f - gap + Noise(distance, side + 8) * 12f;
            if (absolute <= RidgeWidth) return Mathf.Lerp(mid, far, Mathf.InverseLerp(26f, RidgeWidth, absolute));
            float outer = surface.height - 9f - gap + Noise(distance, side + 14) * 7f;
            return Mathf.Lerp(far, outer, Mathf.InverseLerp(RidgeWidth, MapHalfWidth, absolute));
        }

        public static bool TryGetMountainSurface(float distance, float worldX, out RaycastHit hit)
        {
            if (!EnvironmentPhysicsReady)
            {
                hit = default;
                return false;
            }
            return Physics.Raycast(new Vector3(worldX, 220f, distance), Vector3.down, out hit, 480f,
                1 << MountainLayer, QueryTriggerInteraction.Ignore);
        }

        public static float DirectionalGrade(Vector3 surfaceNormal, float heading)
        {
            float vertical = Mathf.Max(.001f, surfaceNormal.y);
            Vector2 forward = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
            return -(surfaceNormal.x * forward.x + surfaceNormal.z * forward.y) / vertical;
        }

        public static bool CanClimbMountain(float rise, float directionalGrade)
        {
            return rise <= .72f && directionalGrade <= MaximumClimbGrade;
        }

        static float CanyonDepth(float distance)
        {
            return 11f * Smooth(386f, 405f, distance) * (1f - Smooth(450f, 471f, distance));
        }

        static float Noise(float value, int seed)
        {
            return Mathf.PerlinNoise(value * .1731f + seed * 18.73f, seed * 6.71f + 21.97f);
        }

        static float Smooth(float start, float end, float value)
        {
            float t = Mathf.Clamp01((value - start) / (end - start));
            return t * t * (3f - 2f * t);
        }

        static float Window(float value, float start, float full, float fade, float end)
        {
            return Smooth(start, full, value) * (1f - Smooth(fade, end, value));
        }

        static float Ramp(float d, float start, float crest, float end, float height)
        {
            if (d <= start || d >= end) return 0f;
            return height * (d <= crest ? (d - start) / (crest - start) : (end - d) / (end - crest));
        }
    }

    /// <summary>
    /// Kinematic arcade motorcycle, deliberately independent of Input, transforms, WheelColliders or networking.
    /// Positive steer/balance/lean mean right. Lean is radians; clutch=1 disconnects the engine.
    /// Call Step once per simulation update; shift, ignition and reset are one-shot input events.
    /// </summary>
    public sealed class MotorSimulation
    {
        public BikeSnapshot State { get; private set; }

        const float IdleRpm = 1350f;
        const float RedlineRpm = 8500f;
        const float WheelRadius = .34f;
        const float Wheelbase = 1.45f;
        const float CentreOfMassHeight = 1.05f;
        const float RiderBalanceTorque = 8.0f;
        const float FinalDrive = 4.8f;
        const float MaximumStep = 1f / 120f;
        static readonly float[] GearRatios = { 0f, 3.0f, 2.2f, 1.7f, 1.35f, 1.08f };
        readonly bool[] obstacleHandled = new bool[MotorCourse.Obstacles.Length];
        float stallTime, messageTime, previousGroundSlope;

        public MotorSimulation()
        {
            ResetRun();
        }

        /// <summary>
        /// Keyboard steering is capped by a lateral-acceleration budget. This preserves useful
        /// parking steering while preventing high-speed input from asking more of the balance
        /// player than the available weight-shift torque can physically supply.
        /// </summary>
        public static float SteeringLimitRadians(float speed)
        {
            speed = Mathf.Clamp(speed, 0f, 43f);
            float lateralAccelerationBudget = Mathf.Lerp(3.6f, 5.8f, Mathf.Clamp01(speed / 22f));
            float geometryLimit = Mathf.Atan(lateralAccelerationBudget * Wheelbase / Mathf.Max(speed * speed, 2.25f));
            return Mathf.Min(24f * Mathf.Deg2Rad, geometryLimit);
        }

        public void ResetRun()
        {
            State = new BikeSnapshot
            {
                gear = 0,
                rpm = IdleRpm,
                engineRunning = true,
                grounded = true,
                elevation = MotorCourse.Sample(0f).height,
                heading = MotorCourse.CourseHeading(0f),
                message = "Debriyaji cek, 1. vitese gec; gaz verirken debriyaji birak."
            };
            ResetTransientState();
            messageTime = 8f;
        }

        public void Respawn()
        {
            int checkpoint = Mathf.Clamp(State.checkpoint, 0, MotorCourse.Checkpoints.Length - 1);
            float elapsed = State.elapsed;
            int crashes = State.crashes;
            float distance = MotorCourse.Checkpoints[checkpoint];
            State = new BikeSnapshot
            {
                distance = distance,
                elevation = MotorCourse.Sample(distance).height,
                gear = 0,
                checkpoint = checkpoint,
                crashes = crashes,
                elapsed = elapsed,
                rpm = IdleRpm,
                engineRunning = true,
                grounded = true,
                heading = MotorCourse.CourseHeading(distance),
                message = checkpoint == 0 ? "Baslangictasiniz. Debriyaj + 1. vites." : "Kontrol noktasindasiniz. Debriyaj + 1. vites."
            };
            ResetTransientState();
            messageTime = 5f;
        }

        void ResetTransientState()
        {
            stallTime = messageTime = 0f;
            previousGroundSlope = GroundSlopeAlongTravel(State.distance, WorldX(State), State.heading);
            Array.Clear(obstacleHandled, 0, obstacleHandled.Length);
        }

        public void Step(MotorInput input, float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            if (input.reset)
            {
                Respawn();
                return;
            }
            input.throttle = ClampFinite(input.throttle, 0f, 1f);
            input.brake = ClampFinite(input.brake, 0f, 1f);
            input.clutch = ClampFinite(input.clutch, 0f, 1f);
            input.steer = ClampFinite(input.steer, -1f, 1f);
            input.balance = ClampFinite(input.balance, -1f, 1f);
            State.gear = Mathf.Clamp(State.gear, 0, 5);

            // Bound recovery after a suspended application; normal frame sizes use identical 120 Hz substeps.
            float remaining = Mathf.Min(dt, .25f);
            if (!State.crashed) ApplyEvents(input);
            while (remaining > .000001f)
            {
                float step = Mathf.Min(remaining, MaximumStep);
                Integrate(input, step);
                remaining -= step;
            }
        }

        void ApplyEvents(MotorInput input)
        {
            if (input.shift != 0)
            {
                if (input.clutch <= .65f)
                    Say("Vites icin debriyaji cek!", 2f);
                else
                {
                    int target = Mathf.Clamp(State.gear + (input.shift > 0 ? 1 : -1), 0, 5);
                    if (target != State.gear)
                    {
                        State.gear = target;
                        Say(target == 0 ? "Bos vites (N)." : target + ". vites", 1.25f);
                    }
                }
            }

            if (input.ignition && !State.engineRunning)
            {
                if (State.gear == 0 || input.clutch > .65f)
                {
                    State.engineRunning = true;
                    State.rpm = IdleRpm;
                    stallTime = 0f;
                    Say("Motor calisti. Gazla birlikte debriyaji yavas birak.", 3f);
                }
                else Say("Mars icin debriyaji cek veya bosa al.", 2.5f);
            }
        }

        void Integrate(MotorInput input, float dt)
        {
            State.elapsed += dt;
            if (State.crashed) return;
            messageTime = Mathf.Max(0f, messageTime - dt);
            if (messageTime <= 0f) State.message = State.engineRunning ? string.Empty : "Motor stop etti. Debriyaj + mars.";

            float oldDistance = State.distance;
            float oldWorldX = WorldX(State);
            CourseSample oldSurface = MotorCourse.Sample(oldDistance);
            bool onRoad = MotorCourse.IsOnRoad(oldDistance, State.lateral);
            float slope = GroundSlopeAlongTravel(oldDistance, oldWorldX, State.heading);
            float engagement = State.gear == 0 ? 0f : 1f - input.clutch;
            float wheelRpm = State.speed / (2f * Mathf.PI * WheelRadius) * 60f * FinalDrive * GearRatios[State.gear];
            float driveAcceleration = 0f;

            if (State.engineRunning)
            {
                float freeRpm = Mathf.Lerp(IdleRpm, RedlineRpm - 150f, input.throttle);
                float coupledRpm = Mathf.Lerp(freeRpm, wheelRpm, engagement * engagement);
                State.rpm = Mathf.Lerp(State.rpm, coupledRpm, 1f - Mathf.Exp(-7f * dt));
                State.rpm = Mathf.Clamp(State.rpm, 0f, RedlineRpm + 250f);

                // Fully engaged at idle can stall. Slipping the clutch keeps revs up for a hill start.
                if (State.gear > 0 && engagement > .82f && State.rpm < 680f)
                    stallTime += dt;
                else stallTime = Mathf.Max(0f, stallTime - 2f * dt);

                if (stallTime > .55f)
                {
                    State.engineRunning = false;
                    State.rpm = 0f;
                    Say("Motor stop etti. Debriyaji cek, marsa bas.", 5f);
                }
                else if (State.gear > 0 && wheelRpm < RedlineRpm)
                {
                    float torque = Mathf.Lerp(.62f, 1f, Mathf.Clamp01(State.rpm / 4300f));
                    float limiter = 1f - Mathf.Clamp01((wheelRpm - 7800f) / 700f);
                    driveAcceleration = input.throttle * 8.2f * (GearRatios[State.gear] / GearRatios[1]) * engagement * torque * limiter;
                }
            }
            else State.rpm = 0f;

            float drag = State.speed > 0f ? .18f + .0048f * State.speed * State.speed + (onRoad ? 0f : .85f) : 0f;
            float engineBrake = State.speed > .1f ? engagement * (1f - input.throttle) * .80f : 0f;
            float gradeAcceleration = State.grounded ? slope * 4.2f : 0f;
            float traction = State.grounded ? (onRoad ? 1f : .78f) : .12f;
            float acceleration = driveAcceleration * traction - input.brake * (onRoad ? 13.5f : 10f) - drag - engineBrake - gradeAcceleration;
            State.speed = Mathf.Clamp(State.speed + acceleration * dt, 0f, 43f);

            float speedSteer = SteeringLimitRadians(State.speed);
            float targetSteering = input.steer * speedSteer;
            float steeringResponse = Mathf.Lerp(.78f, .42f, Mathf.Clamp01(State.speed / 25f));
            State.steeringAngle = Mathf.MoveTowards(State.steeringAngle, targetSteering, steeringResponse * dt);
            float yawRate = State.speed < .05f ? 0f : State.speed / Wheelbase * Mathf.Tan(State.steeringAngle) * Mathf.Cos(State.lean);
            yawRate = Mathf.Clamp(yawRate, -1.85f, 1.85f);
            State.heading = NormalizeAngle(State.heading + yawRate * dt);
            float travel = State.speed * dt;
            float newWorldX = oldWorldX + Mathf.Sin(State.heading) * travel;
            State.distance += Mathf.Cos(State.heading) * travel;
            CourseSample surface = MotorCourse.Sample(State.distance);
            State.lateral = newWorldX - surface.centerX;

            if (travel > .0001f && MotorCourse.EnvironmentPhysicsReady)
            {
                Vector3 start = new Vector3(oldWorldX, State.elevation + .68f, oldDistance);
                Vector3 delta = new Vector3(newWorldX - oldWorldX, 0f, State.distance - oldDistance);
                if (Physics.SphereCast(start, .34f, delta.normalized, out _, delta.magnitude,
                    1 << MotorCourse.TreeLayer, QueryTriggerInteraction.Ignore))
                {
                    RestorePosition(oldDistance, oldWorldX);
                    Crash("Agaca carptiniz! Govdelerin etrafindan dolanin.");
                    return;
                }
            }

            // A steered upright motorcycle falls to the outside of the turn. The balancing player
            // must shift mass into the turn; there is no artificial upright spring.
            float moving = Mathf.Clamp01(State.speed / 5f);
            float roadBank = MotorCourse.IsOnRoad(State.distance, State.lateral) ? surface.bank : 0f;
            float turnAcceleration = State.speed * yawRate;
            float disturbance = .018f * Mathf.Sin(State.elapsed * 1.55f)
                + moving * surface.wind * .30f
                + (onRoad ? 0f : moving * .11f * Mathf.Sin(State.distance * .47f + State.lateral * .31f));
            float rollAcceleration = 9.81f / CentreOfMassHeight * Mathf.Sin(State.lean - roadBank)
                - turnAcceleration / CentreOfMassHeight * Mathf.Cos(State.lean)
                + input.balance * RiderBalanceTorque + disturbance - 1.55f * State.leanVelocity;
            State.leanVelocity = Mathf.Clamp(State.leanVelocity + rollAcceleration * dt, -3.2f, 3.2f);
            State.lean += State.leanVelocity * dt;

            float baseGroundHeight = MotorCourse.BaseGroundHeight(State.distance, newWorldX);
            float groundHeight = MotorCourse.GroundHeight(State.distance, newWorldX);
            if (State.grounded && groundHeight > baseGroundHeight + .03f
                && MotorCourse.TryGetMountainSurface(State.distance, newWorldX, out var mountainHit))
            {
                float climbGrade = MotorCourse.DirectionalGrade(mountainHit.normal, State.heading);
                if (!MotorCourse.CanClimbMountain(groundHeight - State.elevation, climbGrade))
                {
                    RestorePosition(oldDistance, oldWorldX);
                    Crash("Dag yamaci cok dik! Daha yatay bir rota secin.");
                    return;
                }
            }
            float newSlope = GroundSlopeAlongTravel(State.distance, newWorldX, State.heading);
            UpdateElevation(groundHeight, newSlope, dt);
            if (State.crashed) return;

            if (Mathf.Abs(State.lean) > 1.12f)
            {
                Crash("Motor devrildi! Dengeden sorumlu oyuncu ters yone agirlik versin.");
                return;
            }

            CheckObstacles(oldDistance, oldWorldX);
            if (State.crashed) return;
            int nextCheckpoint = State.checkpoint + 1;
            if (nextCheckpoint < MotorCourse.Checkpoints.Length && oldDistance < MotorCourse.Checkpoints[nextCheckpoint]
                && State.distance >= MotorCourse.Checkpoints[nextCheckpoint] && Mathf.Abs(State.lateral) < surface.width * .5f + 2f)
            {
                State.checkpoint = nextCheckpoint;
                Say("Kontrol noktasi! Takimin ilerlemesi kaydedildi.", 3.5f);
            }
            if (!State.finished && oldDistance < MotorCourse.Length && State.distance >= MotorCourse.Length
                && Mathf.Abs(State.lateral) < surface.width * .5f + 3f)
            {
                State.finished = true;
                Say("Zirveye birlikte ulastiniz! Motor serbest; haritayi gezmeye devam edebilirsiniz.", 6f);
            }
        }

        void UpdateElevation(float groundHeight, float slope, float dt)
        {
            if (State.grounded)
            {
                if (State.elevation - groundHeight > .40f)
                {
                    State.grounded = false;
                    State.verticalSpeed = previousGroundSlope * State.speed;
                    State.elevation += State.verticalSpeed * dt - 4.905f * dt * dt;
                    State.verticalSpeed -= 9.81f * dt;
                }
                // A sharp change at a rendered ramp crest produces a ballistic hop.
                else if (previousGroundSlope > .10f && previousGroundSlope - slope > .065f && State.speed > 7f)
                {
                    State.grounded = false;
                    State.verticalSpeed = previousGroundSlope * State.speed + .95f;
                    State.elevation = Mathf.Max(State.elevation, groundHeight) + State.verticalSpeed * dt;
                }
                else
                {
                    State.elevation = groundHeight;
                    State.verticalSpeed = slope * State.speed;
                }
            }
            else
            {
                State.elevation += State.verticalSpeed * dt - 4.905f * dt * dt;
                State.verticalSpeed -= 9.81f * dt;
                if (State.elevation <= groundHeight)
                {
                    float landingSpeed = -State.verticalSpeed;
                    State.elevation = groundHeight;
                    State.verticalSpeed = 0f;
                    State.grounded = true;
                    if (landingSpeed > 10.5f || (landingSpeed > 3f && Mathf.Abs(State.lean) > .72f))
                        Crash("Sert inis! Rampadan once hiz ve dengeyi ayarlayin.");
                    else if (landingSpeed > 2f)
                    {
                        State.leanVelocity += Mathf.Sin(State.elapsed * 2f) * .12f;
                        State.speed *= .97f;
                    }
                }
            }
            previousGroundSlope = slope;
        }

        void CheckObstacles(float previousDistance, float previousWorldX)
        {
            for (int i = 0; i < MotorCourse.Obstacles.Length; i++)
            {
                CourseObstacle obstacle = MotorCourse.Obstacles[i];
                if (obstacleHandled[i]) continue;
                float radius = obstacle.radius + .38f;
                float obstacleX = MotorCourse.Sample(obstacle.distance).centerX + MotorCourse.ObstacleLateral(obstacle, State.elapsed);
                Vector2 start = new Vector2(previousWorldX, previousDistance);
                Vector2 end = new Vector2(WorldX(State), State.distance);
                Vector2 delta = end - start;
                Vector2 obstaclePoint = new Vector2(obstacleX, obstacle.distance);
                float segment = delta.sqrMagnitude;
                float t = segment < .000001f ? 0f : Mathf.Clamp01(Vector2.Dot(obstaclePoint - start, delta) / segment);
                if ((start + delta * t - obstaclePoint).sqrMagnitude >= radius * radius) continue;
                float heightAboveRoad = State.elevation - MotorCourse.Sample(State.distance).height;
                float obstacleHeight = obstacle.kind == 1 ? .55f : obstacle.kind == 2 ? 1.8f : 1.35f;
                if (heightAboveRoad > obstacleHeight) continue;

                if (obstacle.kind == 1 && State.speed <= 7f)
                {
                    obstacleHandled[i] = true;
                    State.speed *= .80f;
                    State.grounded = false;
                    State.verticalSpeed = 1.4f + State.speed * .11f;
                    State.elevation += .04f;
                    Say("Kutuk gecildi. Yavaslamak ise yaradi!", 2f);
                }
                else
                {
                    Crash(obstacle.kind == 1 ? "Kutuge hizli carptiniz! Yavaslayin veya ustunden atlayin."
                        : obstacle.kind == 2 ? "Hareketli engele carptiniz. Acikligi bekleyin!" : "Kayaya carptiniz! Direksiyonla bosluktan gecin.");
                    return;
                }
            }
        }

        void Crash(string reason)
        {
            if (State.crashed) return;
            State.crashed = true;
            State.crashes++;
            State.speed = 0f;
            State.engineRunning = false;
            State.rpm = 0f;
            State.message = reason;
        }

        void Say(string message, float seconds)
        {
            State.message = message;
            messageTime = seconds;
        }

        static float WorldX(BikeSnapshot state)
        {
            return MotorCourse.Sample(state.distance).centerX + state.lateral;
        }

        void RestorePosition(float distance, float worldX)
        {
            State.distance = distance;
            State.lateral = worldX - MotorCourse.Sample(distance).centerX;
        }

        static float GroundSlopeAlongTravel(float distance, float worldX, float heading)
        {
            const float halfSample = .15f;
            float dx = Mathf.Sin(heading) * halfSample;
            float dz = Mathf.Cos(heading) * halfSample;
            return (MotorCourse.GroundHeight(distance + dz, worldX + dx) - MotorCourse.GroundHeight(distance - dz, worldX - dx)) / (2f * halfSample);
        }

        static float NormalizeAngle(float angle)
        {
            while (angle > Mathf.PI) angle -= Mathf.PI * 2f;
            while (angle < -Mathf.PI) angle += Mathf.PI * 2f;
            return angle;
        }

        static float ClampFinite(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, min, max);
        }
    }
}
