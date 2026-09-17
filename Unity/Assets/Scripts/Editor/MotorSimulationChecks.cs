using System;
using UnityEditor;
using UnityEngine;

namespace DortCuce.UnityGame
{
    /// <summary>Focused behavioural checks; callable from the Unity menu or -executeMethod.</summary>
    public static class MotorSimulationChecks
    {
        static int passes;

        [MenuItem("Motor Co-op/Run Simulation Checks")]
        public static void Run()
        {
            passes = 0;
            MotorCourse.EnvironmentPhysicsReady = false;
            StartsNeutralAndClutchDisconnects();
            ShiftsRequireClutchAndAreBounded();
            ClutchLaunchAndStallRecovery();
            SteeringAndBalanceHaveIndependentDirections();
            FreeRoamUsesWorldHeading();
            BrakingCannotReverse();
            CrashCheckpointAndReset();
            ObstacleFootprintsAndSlowLog();
            RampProducesAirborneFlight();
            FinishIsStableAndRestartable();
            FixedFrameRateAgreement();
            CourseHasUsableGeometry();
            MountainClimbRulesRejectOnlySteepFaces();
            PhysicalTreeColliderStopsMotorcycle();
            SteeringForceFitsBalanceAuthority();
            FullRouteIsControllable();
            Debug.Log("MOTOR_SIMULATION_CHECKS_PASS " + passes + "/16");
        }

        static void StartsNeutralAndClutchDisconnects()
        {
            MotorSimulation sim = new MotorSimulation();
            Require(sim.State.gear == 0 && sim.State.engineRunning && sim.State.grounded, "Start must be neutral, running and grounded.");
            Advance(sim, new MotorInput { throttle = 1f }, 2f);
            Near(sim.State.speed, 0f, .001f, "Neutral must not drive the rear wheel.");
            Require(sim.State.rpm > 7500f, "Neutral throttle should rev the engine.");
            sim.Step(new MotorInput { clutch = 1f, shift = 1 }, 1f / 60f);
            Advance(sim, new MotorInput { throttle = 1f, clutch = 1f }, 1f);
            Near(sim.State.speed, 0f, .001f, "Disengaged clutch must not propel the motorcycle.");
            Pass("Neutral and disengaged clutch disconnect drive.");
        }

        static void ShiftsRequireClutchAndAreBounded()
        {
            MotorSimulation sim = new MotorSimulation();
            sim.Step(new MotorInput { shift = 1 }, .02f);
            Require(sim.State.gear == 0 && sim.State.message.Contains("debriyaj"), "Clutchless shift must be rejected and explained.");
            sim.Step(new MotorInput { clutch = .65f, shift = 1 }, .02f);
            Require(sim.State.gear == 0, "Clutch engagement threshold must be strict.");
            for (int i = 0; i < 8; i++) sim.Step(new MotorInput { clutch = 1f, shift = 1 }, .02f);
            Require(sim.State.gear == 5, "Gear must stop at fifth.");
            sim.Step(new MotorInput { clutch = 1f, shift = -99 }, .02f);
            Require(sim.State.gear == 4, "One shift event must change exactly one gear.");
            for (int i = 0; i < 8; i++) sim.Step(new MotorInput { clutch = 1f, shift = -1 }, .02f);
            Require(sim.State.gear == 0, "Gear must stop at neutral.");
            Pass("Clutch requirement, one-shot shifts and gear bounds.");
        }

        static void ClutchLaunchAndStallRecovery()
        {
            MotorSimulation sim = new MotorSimulation();
            sim.Step(new MotorInput { clutch = 1f, shift = 1 }, .02f);
            Advance(sim, new MotorInput { throttle = .7f, clutch = .45f }, 1.5f);
            Require(sim.State.speed > 2f && sim.State.engineRunning, "Slipping clutch with throttle must launch successfully.");
            Advance(sim, new MotorInput { throttle = .7f }, 1f);
            Require(sim.State.speed > 4f && sim.State.engineRunning, "Releasing clutch after launch must continue driving.");

            sim.ResetRun();
            sim.Step(new MotorInput { clutch = 1f, shift = 1 }, .02f);
            Advance(sim, new MotorInput(), 1.5f);
            Require(!sim.State.engineRunning && sim.State.rpm == 0f, "Engaging first at rest without throttle must stall.");
            sim.Step(new MotorInput { ignition = true }, .02f);
            Require(!sim.State.engineRunning, "Starter must require neutral or disengaged clutch.");
            sim.Step(new MotorInput { ignition = true, clutch = 1f }, .02f);
            Require(sim.State.engineRunning && sim.State.rpm > 1000f, "Clutch and ignition must restart stalled engine.");
            sim.State.engineRunning = false;
            sim.Step(new MotorInput { clutch = 1f, shift = -1 }, .02f);
            sim.Step(new MotorInput { ignition = true }, .02f);
            Require(sim.State.gear == 0 && sim.State.engineRunning, "Neutral starter recovery must work too.");
            Pass("Clutch launch, idle stall and both legal restart routes.");
        }

        static void SteeringAndBalanceHaveIndependentDirections()
        {
            MotorSimulation right = Rolling(8f), left = Rolling(8f), neutral = Rolling(8f);
            Advance(right, new MotorInput { balance = 1f, clutch = 1f }, .3f);
            Advance(left, new MotorInput { balance = -1f, clutch = 1f }, .3f);
            Advance(neutral, new MotorInput { clutch = 1f }, .3f);
            Require(right.State.lean > neutral.State.lean + .05f, "Right balance must increase right lean.");
            Require(left.State.lean < neutral.State.lean - .05f, "Left balance must increase left lean.");

            right = Rolling(8f); left = Rolling(8f);
            Advance(right, new MotorInput { steer = .22f, clutch = 1f }, .35f);
            Advance(left, new MotorInput { steer = -.22f, clutch = 1f }, .35f);
            Require(right.State.heading > .02f && left.State.heading < -.02f, "Steering signs must change free world heading.");
            Require(right.State.lateral > .02f && left.State.lateral < -.02f, "Steering must produce world-space lateral travel.");
            Require(right.State.lean < -.004f && left.State.lean > .004f, "Turning an upright motorcycle must disturb roll toward the outside.");

            MotorSimulation correction = Rolling(8f);
            correction.State.lean = .35f;
            Advance(correction, new MotorInput { balance = -1f, clutch = 1f }, .45f);
            Require(correction.State.lean < .22f && !correction.State.crashed, "Opposite balance must recover a moderate lean.");
            Pass("Steering creates physical roll disturbance and player balance can recover it.");
        }

        static void FreeRoamUsesWorldHeading()
        {
            MotorSimulation sim = Rolling(9f);
            Place(sim, 80f, 0f);
            sim.State.heading = 70f * Mathf.Deg2Rad;
            for (int i = 0; i < 180 && !sim.State.crashed; i++)
            {
                float balance = Mathf.Clamp(-sim.State.lean * 1.55f - sim.State.leanVelocity * .42f, -1f, 1f);
                sim.Step(new MotorInput { clutch = 1f, balance = balance }, 1f / 60f);
            }
            Require(!sim.State.crashed, "Leaving asphalt must not cause an artificial crash.");
            Require(Mathf.Abs(sim.State.lateral) > MotorCourse.Sample(sim.State.distance).width * .5f + 10f, "Free heading must allow riding deep into the map terrain.");
            float beforeReturn = sim.State.distance;
            sim.State.heading = Mathf.PI;
            for (int i = 0; i < 60 && !sim.State.crashed; i++)
            {
                float balance = Mathf.Clamp(-sim.State.lean * 1.55f - sim.State.leanVelocity * .42f, -1f, 1f);
                sim.Step(new MotorInput { clutch = 1f, balance = balance }, 1f / 60f);
            }
            Require(sim.State.distance < beforeReturn - 4f, "Turning around must allow travel toward the start instead of forcing forward progress.");
            Pass("Whole-map free roam supports off-road travel and reverse course direction.");
        }

        static void BrakingCannotReverse()
        {
            MotorSimulation braking = Rolling(12f), coasting = Rolling(12f);
            Advance(braking, new MotorInput { brake = 1f, clutch = 1f }, 1.5f);
            Advance(coasting, new MotorInput { clutch = 1f }, 1.5f);
            Near(braking.State.speed, 0f, .001f, "Full braking must stop the motorcycle.");
            Require(coasting.State.speed > 9f, "Coasting reference should retain substantial momentum.");
            float stoppedDistance = braking.State.distance;
            Advance(braking, new MotorInput { brake = 1f, clutch = 1f }, 1f);
            Near(braking.State.distance, stoppedDistance, .001f, "Holding brake at rest must not reverse.");
            Pass("Braking stops, never reverses, and differs from coast drag.");
        }

        static void CrashCheckpointAndReset()
        {
            MotorSimulation sim = Rolling(8f);
            Place(sim, 179.9f, 0f);
            sim.Step(new MotorInput { clutch = 1f }, .05f);
            Require(sim.State.checkpoint == 1, "Passing first checkpoint must save its index.");
            sim.State.lean = 1.15f;
            sim.Step(new MotorInput { clutch = 1f }, .02f);
            Require(sim.State.crashed && sim.State.crashes == 1, "Falling must count exactly one crash.");
            Advance(sim, new MotorInput(), .5f);
            Require(sim.State.crashes == 1, "A frozen crashed state must not count repeated crashes.");
            float elapsed = sim.State.elapsed;
            sim.Respawn();
            Near(sim.State.distance, 180f, .001f, "Respawn must use last checkpoint distance.");
            Near(sim.State.elapsed, elapsed, .001f, "Respawn must preserve elapsed race time.");
            Require(sim.State.crashes == 1 && sim.State.gear == 0 && sim.State.engineRunning && !sim.State.crashed, "Respawn must restore a rideable neutral state and preserve crash count.");
            sim.ResetRun();
            Require(sim.State.checkpoint == 0 && sim.State.crashes == 0 && sim.State.elapsed == 0f && sim.State.distance == 0f, "New run must clear checkpoint, crash and time history.");
            Near(sim.State.elevation, MotorCourse.Sample(0f).height, .001f, "Reset elevation must match course surface.");
            Pass("Crash counting, checkpoint respawn and full reset are distinct.");
        }

        static void ObstacleFootprintsAndSlowLog()
        {
            MotorSimulation hit = Rolling(8f);
            Place(hit, 111f, -2.5f);
            hit.Step(new MotorInput { clutch = 1f }, .02f);
            Require(hit.State.crashed, "Rock collision must use its rendered lateral location.");
            MotorSimulation avoid = Rolling(8f);
            Place(avoid, 111f, 1.5f);
            Advance(avoid, new MotorInput { clutch = 1f }, .4f);
            Require(!avoid.State.crashed, "Open side of a rock must remain traversable.");

            MotorSimulation slow = Rolling(5f);
            Place(slow, 268f, -1.6f);
            slow.Step(new MotorInput { clutch = 1f }, .02f);
            Require(!slow.State.crashed && !slow.State.grounded && slow.State.verticalSpeed > 0f, "Slow log crossing must make a safe bump.");
            MotorSimulation fast = Rolling(11f);
            Place(fast, 268f, -1.6f);
            fast.Step(new MotorInput { clutch = 1f }, .02f);
            Require(fast.State.crashed, "Fast log impact must penalize ignoring braking.");
            Pass("Visible obstacle footprint, avoidance and slow log crossing.");
        }

        static void RampProducesAirborneFlight()
        {
            MotorSimulation sim = Rolling(12f);
            Place(sim, 488.5f, 0f);
            bool airborne = false, landed = false;
            for (int i = 0; i < 180; i++)
            {
                sim.Step(new MotorInput { clutch = 1f, balance = -sim.State.lean * 2f - sim.State.leanVelocity }, 1f / 120f);
                airborne |= !sim.State.grounded;
                if (airborne && sim.State.grounded) { landed = true; break; }
            }
            Require(airborne, "Crossing a ramp crest at speed must launch into flight.");
            Require(landed && !sim.State.crashed, "A balanced moderate-speed ramp jump must land safely.");
            Near(sim.State.elevation, MotorCourse.Sample(sim.State.distance).height, .001f, "Landing must meet rendered road height.");
            Pass("Ramp launch, ballistic airborne state and safe landing.");
        }

        static void FinishIsStableAndRestartable()
        {
            MotorSimulation sim = Rolling(8f);
            Place(sim, 899.95f, 0f);
            sim.State.checkpoint = 4;
            sim.Step(new MotorInput { clutch = 1f }, .02f);
            Require(sim.State.finished && sim.State.speed > 0f && !sim.State.crashed, "Finish must record success without locking the motorcycle.");
            float distance = sim.State.distance;
            Advance(sim, new MotorInput { throttle = 1f }, 1f);
            Require(sim.State.distance > distance, "Free riding must continue beyond the finish line.");
            sim.Respawn();
            Require(!sim.State.crashed && sim.State.distance == 720f && sim.State.checkpoint == 4, "Recovery after finish must return to the last camp without erasing the run.");
            Pass("Finish records success while keeping the motorcycle free to explore.");
        }

        static void FixedFrameRateAgreement()
        {
            MotorSimulation low = new MotorSimulation(), high = new MotorSimulation();
            MotorInput shift = new MotorInput { clutch = 1f, shift = 1 };
            low.Step(shift, 1f / 60f); high.Step(shift, 1f / 60f);
            MotorInput launch = new MotorInput { throttle = .7f, clutch = .35f, balance = -.01f };
            for (int i = 0; i < 90; i++) low.Step(launch, 1f / 30f);
            for (int i = 0; i < 180; i++) high.Step(launch, 1f / 60f);
            Near(low.State.distance, high.State.distance, .005f, "30/60 FPS distance should agree under constant input.");
            Near(low.State.speed, high.State.speed, .005f, "30/60 FPS speed should agree under constant input.");
            Near(low.State.lean, high.State.lean, .001f, "30/60 FPS roll should agree under constant input.");
            float distance = low.State.distance;
            low.Step(new MotorInput(), float.NaN);
            Near(low.State.distance, distance, .0001f, "Non-finite frame time must not corrupt simulation.");
            Pass("120 Hz substeps agree at 30/60 FPS; invalid time is ignored.");
        }

        static void CourseHasUsableGeometry()
        {
            for (int d = 0; d <= MotorCourse.Length; d++)
            {
                CourseSample sample = MotorCourse.Sample(d);
                Require(sample.width >= 2.99f && !float.IsNaN(sample.height), "Every course metre needs a finite surface and usable width.");
            }
            Require(MotorCourse.Sample(425f).width < 3.1f && MotorCourse.Sample(20f).width > 9f, "Bridge must be visibly narrower than tutorial road.");
            Require(Mathf.Abs(MotorCourse.Sample(340f).wind) > .5f, "Wind zone must challenge the balancing player.");
            CourseObstacle gate = new CourseObstacle(657f, 0f, .85f, 2);
            Require(Mathf.Abs(MotorCourse.ObstacleLateral(gate, 0f) - MotorCourse.ObstacleLateral(gate, 2f)) > .2f, "Moving gate must expose deterministic changing position.");
            Pass("Finite course, bridge, wind and deterministic moving gates.");
        }

        static void FullRouteIsControllable()
        {
            MotorSimulation sim = new MotorSimulation();
            // Same launch sequence as a player: hold clutch, select first, rev, release clutch.
            sim.Step(new MotorInput { clutch = 1f, shift = 1 }, 1f / 60f);
            Advance(sim, new MotorInput { clutch = 1f, throttle = 1f }, .5f);
            bool launched = false, shifted = false, crossedBridge = false, jumped = false;
            for (int frame = 0; frame < 15000 && !sim.State.finished && !sim.State.crashed; frame++)
            {
                BikeSnapshot state = sim.State;
                CourseSample surface = MotorCourse.Sample(state.distance);
                float desiredLateral = 0f;
                for (int i = 0; i < MotorCourse.Obstacles.Length; i++)
                {
                    CourseObstacle obstacle = MotorCourse.Obstacles[i];
                    float ahead = obstacle.distance - state.distance;
                    if (obstacle.kind != 2 || ahead > 48f || ahead < -3f) continue;
                    float arrival = state.elapsed + Mathf.Max(0f, ahead) / Mathf.Max(6f, state.speed);
                    float gateAtArrival = MotorCourse.ObstacleLateral(obstacle, arrival);
                    desiredLateral = (gateAtArrival >= 0f ? -2.8f : 2.8f) * Mathf.Clamp01((48f - ahead) / 20f);
                    break;
                }

                float worldX = surface.centerX + state.lateral;
                float targetDistance = state.distance + Mathf.Lerp(12f, 24f, Mathf.Clamp01(state.speed / 15f));
                float targetX = MotorCourse.Sample(targetDistance).centerX + desiredLateral;
                float desiredHeading = Mathf.Atan2(targetX - worldX, targetDistance - state.distance);
                float headingError = Mathf.DeltaAngle(state.heading * Mathf.Rad2Deg, desiredHeading * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                // The controller now uses the full normalized input range; the motorcycle itself
                // applies the speed-sensitive physical steering limit.
                float steer = Mathf.Clamp(headingError * 2.2f - state.lateral * .07f, -1f, 1f);
                float turnAcceleration = state.speed * state.speed / 1.45f * Mathf.Tan(state.steeringAngle);
                float balance = turnAcceleration / (1.05f * 8f) - state.lean * 1.55f - state.leanVelocity * .42f - surface.wind * .038f;
                float targetSpeed = surface.width < 5f ? 8f : (state.distance > 467f && state.distance < 638f ? 12f : 15f);
                MotorInput input = new MotorInput
                {
                    throttle = Mathf.Clamp01(.32f + (targetSpeed - state.speed) * .24f),
                    brake = Mathf.Clamp01((state.speed - targetSpeed - .6f) * .3f),
                    steer = steer,
                    balance = Mathf.Clamp(balance, -1f, 1f),
                    clutch = frame < 50 ? .40f : 0f
                };
                if (state.rpm > 5400f && state.gear < 3 && frame > 60)
                {
                    input.clutch = 1f;
                    input.shift = 1;
                    shifted = true;
                }
                sim.Step(input, 1f / 60f);
                launched |= sim.State.speed > 4f;
                crossedBridge |= sim.State.distance > 475f;
                jumped |= !sim.State.grounded && sim.State.distance > 475f;
            }
            Require(!sim.State.crashed, "Coordinated legal inputs should navigate the full route. " + sim.State.message + " at " + sim.State.distance);
            Require(sim.State.finished && launched && shifted && crossedBridge && jumped, "Full-route input controller must launch, shift, cross bridge, jump and finish without teleporting. Distance=" + sim.State.distance);
            Require(sim.State.checkpoint == 4 && sim.State.crashes == 0, "Completed clean route must collect every checkpoint without crashes.");
            Pass("Entire 900m route is controllable using legal drive, shift, steer and balance inputs.");
        }

        static void MountainClimbRulesRejectOnlySteepFaces()
        {
            float gentle = MotorCourse.DirectionalGrade(new Vector3(0f, .94f, -.342f).normalized, 0f);
            float steep = MotorCourse.DirectionalGrade(new Vector3(0f, .57f, -.82f).normalized, 0f);
            Require(MotorCourse.CanClimbMountain(.18f, gentle), "A roughly 20 degree mountain face must remain rideable.");
            Require(!MotorCourse.CanClimbMountain(.18f, steep), "A roughly 55 degree face must stop the motorcycle.");
            Require(!MotorCourse.CanClimbMountain(.90f, gentle), "A sudden vertical step must not teleport the motorcycle upward.");
            Pass("Gentle mountain faces are climbable; steep faces and vertical steps are solid.");
        }

        static void PhysicalTreeColliderStopsMotorcycle()
        {
            var tree = new GameObject("Test tree trunk");
            try
            {
                tree.layer = MotorCourse.TreeLayer;
                tree.transform.position = new Vector3(0f, 0f, 1.2f);
                var capsule = tree.AddComponent<CapsuleCollider>();
                capsule.radius = .28f;
                capsule.height = 3f;
                capsule.center = new Vector3(0f, 1.5f, 0f);
                Physics.SyncTransforms();
                MotorCourse.EnvironmentPhysicsReady = true;
                MotorSimulation sim = Rolling(8f);
                Advance(sim, new MotorInput { clutch = 1f }, .25f);
                Require(sim.State.crashed && sim.State.message.Contains("Agaca"), "A physical tree trunk must stop the motorcycle.");
                Pass("Tree capsule collision stops the motorcycle instead of allowing pass-through.");
            }
            finally
            {
                MotorCourse.EnvironmentPhysicsReady = false;
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        static void SteeringForceFitsBalanceAuthority()
        {
            float parking = MotorSimulation.SteeringLimitRadians(0f) * Mathf.Rad2Deg;
            float road = MotorSimulation.SteeringLimitRadians(8f) * Mathf.Rad2Deg;
            float fast = MotorSimulation.SteeringLimitRadians(20f) * Mathf.Rad2Deg;
            Require(parking > 20f, "Low-speed manoeuvring still needs useful handlebar travel.");
            Require(road > 4f && road < 7f && fast < 1.5f, "Steering authority must narrow progressively with speed.");

            MotorSimulation sim = Rolling(8f);
            for (int i = 0; i < 120 && !sim.State.crashed; i++)
            {
                BikeSnapshot state = sim.State;
                float yawRate = state.speed / 1.45f * Mathf.Tan(state.steeringAngle);
                float requiredBalance = state.speed * yawRate / (1.05f * 8f)
                    - state.lean * 1.55f - state.leanVelocity * .42f;
                sim.Step(new MotorInput { steer = 1f, balance = Mathf.Clamp(requiredBalance, -1f, 1f), clutch = 1f }, 1f / 60f);
            }
            Require(!sim.State.crashed, "A coordinated full keyboard turn at 8 m/s must stay inside available balance authority.");
            Require(sim.State.heading > .35f && Mathf.Abs(sim.State.lean) < .65f, "Progressive steering must still produce a clear controlled turn.");
            Pass("Speed-sensitive steering remains responsive without overpowering the balance player.");
        }

        static MotorSimulation Rolling(float speed)
        {
            MotorSimulation sim = new MotorSimulation();
            sim.State.speed = speed;
            return sim;
        }

        static void Place(MotorSimulation sim, float distance, float lateral)
        {
            sim.State.distance = distance;
            sim.State.lateral = lateral;
            sim.State.elevation = MotorCourse.Sample(distance).height;
            sim.State.heading = MotorCourse.CourseHeading(distance);
        }

        static void Advance(MotorSimulation sim, MotorInput input, float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * 60f);
            for (int i = 0; i < frames; i++) sim.Step(input, 1f / 60f);
        }

        static void Near(float actual, float expected, float tolerance, string message)
        {
            Require(Mathf.Abs(actual - expected) <= tolerance, message + " Actual=" + actual + "; expected=" + expected);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("MOTOR_SIMULATION_CHECK_FAILED: " + message);
        }

        static void Pass(string message)
        {
            passes++;
            Debug.Log("MOTOR_SIMULATION_PASS " + passes + ": " + message);
        }
    }
}
