using UnityEngine;

namespace DortCuce.UnityGame
{
    /// <summary>
    /// A persistent closed-test mark. It does not provide DRM; it makes an
    /// accidentally redistributed build identifiable and keeps the test terms visible.
    /// </summary>
    public sealed class ClosedDemoMark : MonoBehaviour
    {
        public const string BuildId = "ENGIN-CLOSED-ALPHA-20260916";
        private GUIStyle labelStyle;
        private GUIStyle shadowStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!Application.version.Contains("demo") && !Application.productName.Contains("Closed Playtest")) return;
            var marker = new GameObject("Closed Demo Mark");
            DontDestroyOnLoad(marker);
            marker.AddComponent<ClosedDemoMark>();
        }

        private void OnGUI()
        {
            EnsureStyles();
            const float width = 470f;
            const float height = 25f;
            var area = new Rect(Screen.width - width - 14f, Screen.height - height - 10f, width, height);
            string text = "CLOSED TEST • DO NOT REDISTRIBUTE • " + BuildId;
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, shadowStyle);
            GUI.Label(area, text, labelStyle);
        }

        private void EnsureStyles()
        {
            if (labelStyle != null) return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(1f, 0.86f, 0.35f, 0.82f);

            shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.72f);
        }
    }
}
