using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourTargetRpcVoidReturn : NetworkBehaviour
    {
        [TargetRpc]
        public int TargetRpcCantHaveNonVoidReturn()
        {
            return 1;
        }
    }
}
