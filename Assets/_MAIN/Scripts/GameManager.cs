using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GameManager : NetworkBehaviour 
{
	[SyncVar]
	public bool isAnchorAlreadyAdded;

	[ClientRpc]
	public void RpcSyncVarIsAnchorAlreadyAdded (bool isAnchorAlreadyAdded)
	{
		this.isAnchorAlreadyAdded = isAnchorAlreadyAdded;
	}
}
