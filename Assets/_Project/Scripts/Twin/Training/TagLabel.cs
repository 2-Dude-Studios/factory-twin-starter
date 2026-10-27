using TMPro;
using UnityEngine;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Training (M12): the words on the lockout tag. 1910.147(c)(5)(ii)(D) and (c)(5)(iii) want the tag to identify the
    /// employee who applied it and to warn against energizing, so it reads DANGER, DO NOT OPERATE and the trainee's
    /// name from the training controller.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class TagLabel : MonoBehaviour
    {
        [SerializeField] private TrainingController training;

        private void Start()
        {
            string name = training != null ? training.TraineeName : string.Empty;
            GetComponent<TextMeshPro>().text = Words(name);
        }

        public static string Words(string trainee) =>
            string.IsNullOrEmpty(trainee) ? "DANGER\nDO NOT\nOPERATE" : $"DANGER\nDO NOT\nOPERATE\n\n{trainee}";
    }
}
