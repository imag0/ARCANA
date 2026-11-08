using UnityEngine;
namespace Arcana
{
    public sealed class LabStation : MonoBehaviour
    {
        public enum Kind {Winder,Guided,WBenchmark,Crossings}
        public Kind kind;
    }
}
