using System;
using UnityEngine;

namespace DortCuce.UnityGame
{
    /// <summary>Pure role checks, callable from the project's batch verification entry point.</summary>
    public static class CoopRoleChecks
    {
        public static void Run()
        {
            MotorInput all = new MotorInput { throttle = 1, brake = 1, steer = 1, balance = 1,
                clutch = 1, shift = 1, ignition = true, reset = true };
            for (int count = 1; count <= 4; count++)
            {
                int throttle = 0, brake = 0, steering = 0, balance = 0, clutch = 0, shift = 0, ignition = 0, reset = 0;
                for (int slot = 0; slot < count; slot++)
                {
                    MotorInput masked = CoopSession.MaskInput(all, slot, count);
                    throttle += (int)masked.throttle;
                    brake += (int)masked.brake;
                    steering += (int)masked.steer;
                    balance += (int)masked.balance;
                    clutch += (int)masked.clutch;
                    shift += masked.shift;
                    ignition += masked.ignition ? 1 : 0;
                    reset += masked.reset ? 1 : 0;
                    Check(masked.throttle == (slot == 0 ? 1f : 0f), "Throttle belongs to host, count " + count);
                    Check(masked.steer == (count == 4 ? (slot == 2 ? 1f : 0f) : (slot == 0 ? 1f : 0f)), "Steering allocation");
                    Check(masked.balance == (slot == count - 1 ? 1f : 0f), "Balance allocation");
                    Check(masked.clutch == ((count == 1 || slot == 1) ? 1f : 0f), "Transmission allocation");
                    Check(!masked.reset || slot == 0, "Remote reset forbidden");
                    Check(!string.IsNullOrEmpty(CoopSession.RoleLabel(slot, count)), "Every player has a label");
                }
                Check(throttle == 1 && brake == 1 && steering == 1 && balance == 1 && clutch == 1 &&
                    shift == 1 && ignition == 1 && reset == 1, "Every control has exactly one owner at " + count + " players");
            }
            MotorInput bad = CoopSession.MaskInput(new MotorInput { throttle = float.NaN, brake = float.PositiveInfinity,
                steer = -99f, balance = float.NegativeInfinity, clutch = 12f, shift = int.MinValue, reset = true }, 0, 1);
            Check(bad.throttle == 0f && bad.brake == 0f && bad.steer == -1f && bad.balance == 0f &&
                bad.clutch == 1f && bad.shift == -1 && bad.reset, "Malformed input is neutralized or clamped");
            MotorInput invalid = CoopSession.MaskInput(all, -1, 4);
            Check(invalid.throttle == 0 && !invalid.reset && invalid.shift == 0, "Invalid slot is neutral");
            invalid = CoopSession.MaskInput(all, 4, 4);
            Check(invalid.balance == 0 && !invalid.ignition, "Out-of-range slot is neutral");
            invalid = CoopSession.MaskInput(all, 0, 5);
            Check(invalid.throttle == 0 && !invalid.reset, "Out-of-range count is neutral");
            Debug.Log("[CoopRoleChecks] PASS: 1–4 player ownership, host-only reset, malformed input clamps.");
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("CoopRoleChecks: " + message);
        }
    }
}
