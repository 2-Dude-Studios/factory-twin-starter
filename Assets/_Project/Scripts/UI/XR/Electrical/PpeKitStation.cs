using System;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Electrical safety (M27, ahead of M20): the bay's gear bench and the kit on it. cell/elec/gear_up (the start menu's
    /// Electrical card) puts every item back on the bench and starts the PPE stage. Items held to the head or chest
    /// go on in <see cref="PpeDressing"/>'s order: elec/ppe_worn counts them, an item out of order is refused
    /// (elec/ppe_refused, its index) and stays in the hand. The gloves tint the hands. With the kit complete,
    /// elec/ppe_cat goes to the kit's category and E1 starts (cell/elec/scenario 1): the training begins dressed.
    /// </summary>
    public sealed class PpeKitStation : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [Tooltip("The headset camera: the head zone is around it, the body zone below it.")]
        [SerializeField] private Transform head;
        [SerializeField] private PpeItem[] items = Array.Empty<PpeItem>();
        [Tooltip("The hands' renderers, tinted while the gloves are on.")]
        [SerializeField] private Renderer[] hands = Array.Empty<Renderer>();
        [SerializeField] private Color gloveColor = new Color(0.55f, 0.38f, 0.2f);
        [SerializeField, Min(0.05f)] private float headRadius = 0.28f;
        [SerializeField, Min(0.05f)] private float bodyRadius = 0.38f;
        [SerializeField] private float bodyBelowHead = 0.45f;
        [SerializeField, Min(1)] private int firstScenario = 1;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private int worn;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color0 = Shader.PropertyToID("_Color");
        private readonly PpeDressing _dressing = new PpeDressing();
        private IDisposable _subscription;
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            if (context == null)
                return;
            _subscription = context.Bus.Subscribe(CellTopics.ElecGearUp.ToString(), m =>
            {
                if (m.Value.AsNumber != 0d)
                    GearUp();
            });
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        /// <summary>True when a held item at <paramref name="position"/> is where its zone is on the trainee.</summary>
        public bool AtZone(Vector3 position, PpeZone zone)
        {
            if (head == null)
                return false;
            if (zone == PpeZone.Head)
                return Vector3.Distance(position, head.position) <= headRadius;
            Vector3 chest = head.position - new Vector3(0f, bodyBelowHead, 0f);
            return Vector3.Distance(position, chest) <= bodyRadius;
        }

        /// <summary>An item held to its zone: on if it is the next one, refused (and left in the hand) if not.</summary>
        public void Offer(PpeItem item)
        {
            if (item == null || item.Worn)
                return;
            if (!_dressing.TryWear(item.Index))
            {
                Publish(CellTopics.ElecPpeRefused, item.Index);
                return;
            }
            item.PutOn();
            worn = _dressing.Worn;
            Publish(CellTopics.ElecPpeWorn, worn);
            if (item.Index == PpeDressing.Kit.Count - 1)
                TintHands(true);
            if (_dressing.Complete)
            {
                Publish(CellTopics.ElecPpeCategory, PpeDressing.Category);
                Publish(CellTopics.ElecScenario, firstScenario);
                Debug.Log($"[PpeKitStation] PPE category {PpeDressing.Category} on; E{firstScenario} started.", this);
            }
        }

        private void GearUp()
        {
            _dressing.Reset();
            worn = 0;
            foreach (PpeItem item in items)
            {
                if (item != null)
                    item.Restore();
            }
            TintHands(false);
            Publish(CellTopics.ElecPpeCategory, 0);
            Publish(CellTopics.ElecPpeWorn, 0);
            Debug.Log("[PpeKitStation] Gear up: PPE kit on the bench.", this);
        }

        private void TintHands(bool gloves)
        {
            _block ??= new MaterialPropertyBlock();
            foreach (Renderer r in hands)
            {
                if (r == null)
                    continue;
                if (!gloves)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, gloveColor);
                _block.SetColor(Color0, gloveColor);
                r.SetPropertyBlock(_block);
            }
        }

        private void Publish(TopicPath topic, double value) =>
            context.Bus.Publish(topic, SignalValue.FromNumber(value), CellContext.NowUnixMs);
    }
}
