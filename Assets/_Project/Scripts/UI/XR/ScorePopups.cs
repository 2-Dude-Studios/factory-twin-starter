using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Fire safety (M15): the score in the headset. Each point the FireDrillScorer awards floats up in front of the
    /// trainee and fades (green for a right step, red for a mistake); a badge shows larger, with its icon, and a chime.
    /// </summary>
    public sealed class ScorePopups : MonoBehaviour
    {
        [SerializeField] private FireDrillScorer scorer;
        [Tooltip("The headset camera: popups appear in front of it.")]
        [SerializeField] private Transform head;
        [SerializeField] private AudioSource chimeSource;
        [SerializeField] private BadgeIcons badgeIcons;
        [SerializeField, Min(0.5f)] private float distance = 1.3f;
        [SerializeField, Min(0.5f)] private float lifeSeconds = 2.2f;
        [SerializeField] private float riseMetres = 0.22f;

        private static readonly Color Good = new Color(0.35f, 0.9f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f);

        private sealed class Popup
        {
            public TextMeshPro Text;
            public SpriteRenderer Icon;
            public float Born;
            public Vector3 Start;
            public Color Colour;
        }

        private readonly List<Popup> _live = new List<Popup>();
        private readonly Queue<Popup> _pool = new Queue<Popup>();
        private readonly Dictionary<Texture2D, Sprite> _sprites = new Dictionary<Texture2D, Sprite>();
        private AudioClip _chime;
        private int _stack;
        private float _lastShown;

        private void Awake()
        {
            _chime = ProceduralAudio.Clip("BadgeChime", ProceduralAudio.ChimeSamples(0.5f));
        }

        private void OnDestroy()
        {
            if (_chime != null)
                Destroy(_chime);
            foreach (Sprite sprite in _sprites.Values)
                Destroy(sprite);
            _sprites.Clear();
        }

        private void OnEnable()
        {
            if (scorer != null)
                scorer.Scored += Show;
        }

        private void OnDisable()
        {
            if (scorer != null)
                scorer.Scored -= Show;
        }

        private void Show(ScoreEvent e)
        {
            if (head == null || !head.gameObject.activeInHierarchy)
                return;
            // Popups that come together stack upwards instead of overlapping.
            _stack = Time.time - _lastShown < 0.6f ? _stack + 1 : 0;
            _lastShown = Time.time;
            if (e.Points != 0)
            {
                string points = (e.Points > 0 ? "+" : string.Empty) + e.Points.ToString(CultureInfo.InvariantCulture);
                Spawn($"{points}  {e.Text}", e.Mistake ? Bad : Good, 0.045f, null);
            }
            if (e.Badge != null)
            {
                _stack++;
                Texture2D icon = badgeIcons != null ? badgeIcons.IconFor(e.Badge) : null;
                Color colour = badgeIcons != null ? badgeIcons.ColourFor(e.Badge) : Gold;
                Spawn(icon != null ? $"BADGE  {e.Badge}" : $"★ BADGE  {e.Badge}", colour, 0.07f, SpriteFor(icon));
                if (chimeSource != null && _chime != null)
                    chimeSource.PlayOneShot(_chime);
            }
        }

        private void Spawn(string message, Color colour, float size, Sprite icon)
        {
            Popup popup = _pool.Count > 0 ? _pool.Dequeue() : Create();
            TextMeshPro text = popup.Text;
            text.gameObject.SetActive(true);
            text.text = message;
            text.fontSize = size * 10f;
            text.color = colour;
            text.ForceMeshUpdate();
            // The icon sits left of the text, 12 cm tall for a badge.
            popup.Icon.sprite = icon;
            popup.Icon.gameObject.SetActive(icon != null);
            if (icon != null)
            {
                popup.Icon.color = colour;
                float half = text.textBounds.size.x * 0.5f;
                popup.Icon.transform.localPosition = new Vector3(-half - 0.08f, 0f, 0f);
                float side = 0.12f / Mathf.Max(icon.bounds.size.y, 0.0001f);
                popup.Icon.transform.localScale = new Vector3(side, side, 1f);
            }
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = head.forward;
            Vector3 start = head.position + forward * distance + Vector3.down * 0.12f + Vector3.up * (0.09f * _stack);
            text.transform.SetPositionAndRotation(start, Quaternion.LookRotation(forward, Vector3.up));
            popup.Born = Time.time;
            popup.Start = start;
            popup.Colour = colour;
            _live.Add(popup);
        }

        private Sprite SpriteFor(Texture2D texture)
        {
            if (texture == null)
                return null;
            if (!_sprites.TryGetValue(texture, out Sprite sprite))
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.height);
                _sprites[texture] = sprite;
            }
            return sprite;
        }

        private Popup Create()
        {
            var go = new GameObject("ScorePopup");
            go.transform.SetParent(transform, false);
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            // TextMeshPro looks down -Z at the reader; a sprite faces -Z too, so it needs no turn.
            var icon = iconGo.AddComponent<SpriteRenderer>();
            iconGo.SetActive(false);
            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            text.rectTransform.sizeDelta = new Vector2(1.6f, 0.2f);
            return new Popup { Text = text, Icon = icon };
        }

        private void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Popup popup = _live[i];
                float t = (Time.time - popup.Born) / lifeSeconds;
                if (t >= 1f)
                {
                    popup.Text.gameObject.SetActive(false);
                    _pool.Enqueue(popup);
                    _live.RemoveAt(i);
                    continue;
                }
                popup.Text.transform.position = popup.Start + Vector3.up * (riseMetres * t);
                Color colour = popup.Colour;
                colour.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                popup.Text.color = colour;
                if (popup.Icon.gameObject.activeSelf)
                    popup.Icon.color = colour;
            }
        }
    }
}
