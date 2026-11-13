using System;
using Oculus.Platform;
using Oculus.Platform.Models;
using TwoDude.FactoryTwin.Twin;
using UnityEngine;

namespace TwoDude.FactoryTwin.UI
{
    /// <summary>
    /// The headset hub (M28): the trainee's Meta account. On the Quest it starts the Meta Horizon platform with the
    /// App ID from TwinConfig, runs the store's entitlement check, and reads the signed-in user: their display name
    /// becomes the trainee (records, badge book, the hub menu). Without an App ID, off the headset, or if the platform
    /// is not there (a sideload on an account outside the developer organisation), it stays offline and the local name
    /// stands. Meta's rules: platform features answer only for store copies, or for the organisation's own accounts.
    /// </summary>
    public sealed class MetaAccount : MonoBehaviour
    {
        [SerializeField] private CellContext context;
        [SerializeField] private TrainingController lockout;
        [SerializeField] private RobotCellScorer robot;
        [SerializeField] private FireDrillScorer fire;

        [Header("Status (play mode, read-only)")]
        [SerializeField] private bool ready;
        [SerializeField] private bool entitled;
        [SerializeField] private string displayName;
        [SerializeField] private string lastError;

        /// <summary>The platform is up (leaderboards can be used).</summary>
        public bool Ready => ready;
        public string DisplayName => displayName;

        /// <summary>Raised once the signed-in user's name is known.</summary>
        public event Action<string> SignedIn;

        private void Start()
        {
            string appId = context != null && context.Config != null ? context.Config.QuestAppId : string.Empty;
            if (UnityEngine.Application.platform != RuntimePlatform.Android)
                return;
            if (string.IsNullOrWhiteSpace(appId))
            {
                Offline("no App ID in TwinConfig yet");
                return;
            }
            try
            {
                if (!Core.IsInitialized())
                    Core.Initialize(appId.Trim());
            }
            catch (Exception e)
            {
                Offline("platform did not start: " + e.Message);
                return;
            }
            Entitlements.IsUserEntitledToApplication().OnComplete(OnEntitlement);
        }

        private void OnEntitlement(Message message)
        {
            if (message.IsError)
            {
                // A store copy that fails this is not entitled; a sideload outside the organisation fails it too.
                Offline("entitlement check failed: " + message.GetError().Message);
                return;
            }
            entitled = true;
            ready = true;
            Users.GetLoggedInUser().OnComplete(OnUser);
        }

        private void OnUser(Message<User> message)
        {
            if (message.IsError || message.Data == null)
            {
                lastError = message.IsError ? message.GetError().Message : "no user";
                Debug.LogWarning($"[MetaAccount] Signed-in user not read: {lastError}.", this);
                return;
            }
            string name = !string.IsNullOrWhiteSpace(message.Data.DisplayName) ? message.Data.DisplayName : message.Data.OculusID;
            if (string.IsNullOrWhiteSpace(name))
                return;
            displayName = name.Trim();
            if (lockout != null)
                lockout.SetTrainee(displayName);
            if (robot != null)
                robot.SetTrainee(displayName);
            if (fire != null)
                fire.SetTrainee(displayName);
            Debug.Log("[MetaAccount] Signed in; entitled; trainee is the Meta account's display name.", this);
            SignedIn?.Invoke(displayName);
        }

        private void Offline(string why)
        {
            ready = false;
            lastError = why;
            Debug.Log($"[MetaAccount] Offline ({why}); the local trainee name stands.", this);
        }
    }
}
