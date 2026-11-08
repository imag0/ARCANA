using UnityEngine;

namespace Arcana
{
    public sealed class LabSound : MonoBehaviour
    {
        public ExperimentState experiment;public AudioSource source;public AudioClip latch;
        void OnEnable(){experiment.Latched+=Play;}
        void OnDisable(){experiment.Latched-=Play;}
        void Play(){if(source!=null&&latch!=null)source.PlayOneShot(latch,.35f*Elemental.ElementalFeedback.SfxGain);}
    }
}
