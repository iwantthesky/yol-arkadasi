using System;
using UnityEngine;

namespace DortCuce.UnityGame
{
    public readonly struct TrackNode
    {
        public TrackNode(Vector3 position, float width)
        {
            Position = position;
            Width = width;
        }

        public Vector3 Position { get; }
        public float Width { get; }
    }

    public static class DortCuceTrackDefinition
    {
        public const int NodeCount = 82;

        public static TrackNode[] Create()
        {
            var nodes = new TrackNode[NodeCount];

            for (var i = 0; i < nodes.Length; i++)
            {
                var z = i * 9.5f;
                var x = Mathf.Sin(i * 0.24f) * 8.5f + Mathf.Sin(i * 0.071f) * 14f;
                var y = 1.4f + Mathf.Sin(i * 0.31f) * 1.15f;

                if (i >= 19 && i <= 27)
                {
                    y += Mathf.Sin((i - 19) / 8f * Mathf.PI) * 5.5f;
                }

                if (i >= 48 && i <= 57)
                {
                    y += Mathf.Sin((i - 48) / 9f * Mathf.PI) * 8f;
                    x += (i - 48) * 1.1f;
                }

                if (i >= 66)
                {
                    y += (i - 66) * 0.28f;
                }

                var narrow = (i >= 12 && i <= 20) || (i >= 36 && i <= 44) || (i >= 63 && i <= 72);
                var width = narrow ? 3.25f : 6.4f;
                nodes[i] = new TrackNode(new Vector3(x, y, z), width);
            }

            return nodes;
        }

        public static void Validate(TrackNode[] nodes)
        {
            if (nodes == null || nodes.Length != NodeCount)
            {
                throw new InvalidOperationException($"Track requires exactly {NodeCount} nodes.");
            }

            var narrowCount = 0;
            var elevationRange = 0f;
            var minY = float.MaxValue;
            var maxY = float.MinValue;

            for (var i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Width < 4f)
                {
                    narrowCount++;
                }

                minY = Mathf.Min(minY, nodes[i].Position.y);
                maxY = Mathf.Max(maxY, nodes[i].Position.y);

                if (i > 0 && nodes[i].Position.z <= nodes[i - 1].Position.z)
                {
                    throw new InvalidOperationException("Track nodes must advance along Z.");
                }
            }

            elevationRange = maxY - minY;
            if (narrowCount < 20 || elevationRange < 8f)
            {
                throw new InvalidOperationException("Track must contain meaningful narrow and elevated sections.");
            }
        }
    }
}
