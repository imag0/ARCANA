using UnityEngine;
namespace Arcana.Elemental
{
    public sealed class ConductivePost : MonoBehaviour
    {
        public string stableId;public float conductivity=8;public bool insulated,receiver;public double receivedJ;public double requiredJ=2000;public Light indicator;
        public bool Energized=>receivedJ>=requiredJ;
        void Update(){if(indicator!=null){indicator.enabled=Energized;indicator.intensity=Energized?2:0;}}
        public void Receive(double energy){receivedJ+=Mathf.Max(0,(float)energy);}
    }
}
