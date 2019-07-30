using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GameManager : NetworkBehaviour 
{
	[SyncVar]
	public bool isAnchorAlreadyAdded;

	[ClientRpc]
	public void RpcSetAnchor (bool anchorValue)
	{
		isAnchorAlreadyAdded = anchorValue;
	}

	[Command]
	public void CmdSetAnchor (bool anchorValue)
	{
		isAnchorAlreadyAdded = anchorValue;
	}
}
