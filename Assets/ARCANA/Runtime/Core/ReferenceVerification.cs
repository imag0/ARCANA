using System;
using UnityEngine;
namespace Arcana.Core
{
    public static class ReferenceVerification
    {
        [Serializable] sealed class Receipt {public string S1="NOT RUN",D1="NOT RUN";}
        static Receipt receipt;
        public static string Summary
        {
            get{if(receipt==null){var asset=Resources.Load<TextAsset>("ReferenceVerification");receipt=asset!=null?JsonUtility.FromJson<Receipt>(asset.text):new Receipt();}return "REFERENCE FIXTURE · S1: "+receipt.S1+" · D1: "+receipt.D1;}
        }
    }
}
