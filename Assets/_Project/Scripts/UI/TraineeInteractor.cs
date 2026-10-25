using TwoDude.FactoryTwin.Twin;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// Training: lets the desktop trainee use the training points (M11). While the trainee's eye is the live shot,
    /// the point under the crosshair (straight ahead, within reach) lights up and its prompt shows on the HUD; a left
    /// click uses it. The right mouse button stays the look control (TraineeWalker).
    /// </summary>
    public sealed class TraineeInteractor : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera eye;
        [SerializeField] private TraineeHud hud;
        [Tooltip("How far the trainee can reach, in metres: they have to walk up to a point to use it.")]
        [SerializeField, Min(0.5f)] private float reach = 2.5f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private TrainingPoint _hovered;
        private int _mask;

        private void Awake()
        {
            // The zones and the trainee's own body are triggers on their own layers; aim through them.
            _mask = ~LayerMask.GetMask("Trainee", "SafetyZone", "Ignore Raycast");
        }

        private void OnDisable()
        {
            Hover(null);
            if (hud != null)
                hud.SetVisible(false);
        }

        private void Update()
        {
            bool live = eye != null && eye.IsLive;
            if (hud != null)
                hud.SetVisible(live);
            if (!live)
            {
                Hover(null);
                return;
            }

            Hover(PointAhead());
            if (_hovered != null && hud != null)
                hud.SetPrompt(_hovered.Prompt);

            Mouse mouse = Mouse.current;
            if (_hovered != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
                _hovered.Use();
        }

        // The nearest training point straight ahead within reach. Hall geometry is not checked for occlusion: the
        // points sit on the outside of the machine, and the CNC's own box collider would otherwise hide them.
        private TrainingPoint PointAhead()
        {
            Transform view = eye.transform;
            int count = Physics.RaycastNonAlloc(view.position, view.forward, _hits, reach, _mask, QueryTriggerInteraction.Collide);
            TrainingPoint nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance >= nearestDistance)
                    continue;
                TrainingPoint point = _hits[i].collider.GetComponentInParent<TrainingPoint>();
                if (point == null)
                    continue;
                nearest = point;
                nearestDistance = _hits[i].distance;
            }
            return nearest;
        }

        private void Hover(TrainingPoint point)
        {
            if (point == _hovered)
                return;
            if (_hovered != null)
                _hovered.SetHovered(false);
            _hovered = point;
            if (_hovered != null)
                _hovered.SetHovered(true);
            if (hud != null && _hovered == null)
                hud.SetPrompt(string.Empty);
        }
    }
}
