using GoogleARCore;
using UnityEngine;
using UnityEngine.Networking;
using GoogleARCore.Examples.CloudAnchors;

#if UNITY_EDITOR

	// Set up touch input propagation while using Instant Preview in the editor.
	using Input = GoogleARCore.InstantPreviewInput;

#endif

/// <summary>
/// Controller for the Cloud Anchors Example. Handles the ARCore lifecycle.
/// </summary>
public class MyAnchorController : MonoBehaviour
{
	[Header("ARCore")]
	public NetworkManagerUIController networkManagerUIController;
	public GameObject arCoreRootGO;
	public ARCoreWorldOriginHelper arCoreWorldOriginHelper;

	[Header("ARKit")]
	public GameObject arKitRootGO;
	public Camera arKitFirstPersonCamera;
	private ARKitHelper m_ARKitHelper = new ARKitHelper();
	private bool m_IsOriginPlaced = false;
	private bool m_AnchorAlreadyInstantiated = false;
	private bool m_AnchorFinishedHosting = false;
	private bool m_IsQuitting = false;
	private Component m_WorldOriginAnchor = null;
	private Pose? m_LastHitPose = null;
	private ApplicationMode m_CurrentMode = ApplicationMode.Ready;

	#pragma warning disable 618
		private CloudAnchorsNetworkManager m_NetworkManager;
	#pragma warning restore 618

	public enum ApplicationMode
	{
		Ready,
		Hosting,
		Resolving,
	}

	public void Start()
	{

		#pragma warning disable 618
			m_NetworkManager = networkManagerUIController.GetComponent<CloudAnchorsNetworkManager>();
		#pragma warning restore 618

		m_NetworkManager.OnClientConnected += _OnConnectedToServer;
		m_NetworkManager.OnClientDisconnected += _OnDisconnectedFromServer;

		// A Name is provided to the Game Object so it can be found by other Scripts
		// instantiated as prefabs in the scene.
		gameObject.name = "CloudAnchorsExampleController";
		arCoreRootGO.SetActive(false);
		arKitRootGO.SetActive(false);
		_ResetStatus();
	}

	/// <summary>
	/// The Unity Update() method.
	/// </summary>
	public void Update()
	{
		_UpdateApplicationLifecycle();

		// If we are neither in hosting nor resolving mode then the update is complete.
		if (m_CurrentMode != ApplicationMode.Hosting &&
			m_CurrentMode != ApplicationMode.Resolving)
		{
			return;
		}

		// If the origin anchor has not been placed yet, then update in resolving mode is
		// complete.
		if (m_CurrentMode == ApplicationMode.Resolving && !m_IsOriginPlaced)
		{
			return;
		}

		// If the player has not touched the screen then the update is complete.
		Touch touch;
		if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
		{
			return;
		}

		TrackableHit arcoreHitResult = new TrackableHit();
		m_LastHitPose = null;

		// Raycast against the location the player touched to search for planes.
		if (Application.platform != RuntimePlatform.IPhonePlayer)
		{
			if (arCoreWorldOriginHelper.Raycast(touch.position.x, touch.position.y,
					TrackableHitFlags.PlaneWithinPolygon, out arcoreHitResult))
			{
				m_LastHitPose = arcoreHitResult.Pose;
			}
		}
		else
		{
			Pose hitPose;
			if (m_ARKitHelper.RaycastPlane(
				arKitFirstPersonCamera, touch.position.x, touch.position.y, out hitPose))
			{
				m_LastHitPose = hitPose;
			}
		}

		// If there was an anchor placed, then instantiate the corresponding object.
		if (m_LastHitPose != null)
		{
			// The first touch on the Hosting mode will instantiate the origin anchor. Any
			// subsequent touch will instantiate a star, both in Hosting and Resolving modes.
			// if (_CanPlaceStars())
			// {
			// 	_InstantiateStar();
			// }
			// else 
			if (!m_IsOriginPlaced && m_CurrentMode == ApplicationMode.Hosting)
			{
				if (Application.platform != RuntimePlatform.IPhonePlayer)
				{
					m_WorldOriginAnchor =
						arcoreHitResult.Trackable.CreateAnchor(arcoreHitResult.Pose);
				}
				else
				{
					m_WorldOriginAnchor = m_ARKitHelper.CreateAnchor(m_LastHitPose.Value);
				}

				SetWorldOrigin(m_WorldOriginAnchor.transform);
				_InstantiateAnchor();
				OnAnchorInstantiated(true);
			}
		}
	}

	/// <summary>
	/// Sets the apparent world origin so that the Origin of Unity's World Coordinate System
	/// coincides with the Anchor. This function needs to be called once the Cloud Anchor is
	/// either hosted or resolved.
	/// </summary>
	/// <param name="anchorTransform">Transform of the Cloud Anchor.</param>
	public void SetWorldOrigin(Transform anchorTransform)
	{
		if (m_IsOriginPlaced)
		{
			Debug.LogWarning("The World Origin can be set only once.");
			return;
		}

		m_IsOriginPlaced = true;

		if (Application.platform != RuntimePlatform.IPhonePlayer)
		{
			arCoreWorldOriginHelper.SetWorldOrigin(anchorTransform);
		}
		else
		{
			m_ARKitHelper.SetWorldOrigin(anchorTransform);
		}
	}

	/// <summary>
	/// Handles user intent to enter a mode where they can place an anchor to host or to exit
	/// this mode if already in it.
	/// </summary>
	public void OnEnterHostingModeClick()
	{
		if (m_CurrentMode == ApplicationMode.Hosting)
		{
			m_CurrentMode = ApplicationMode.Ready;
			_ResetStatus();
			Debug.Log("Reset ApplicationMode from Hosting to Ready.");
		}

		m_CurrentMode = ApplicationMode.Hosting;
		_SetPlatformActive();
	}

	/// <summary>
	/// Handles a user intent to enter a mode where they can input an anchor to be resolved or
	/// exit this mode if already in it.
	/// </summary>
	public void OnEnterResolvingModeClick()
	{
		if (m_CurrentMode == ApplicationMode.Resolving)
		{
			m_CurrentMode = ApplicationMode.Ready;
			_ResetStatus();
			Debug.Log("Reset ApplicationMode from Resolving to Ready.");
		}

		m_CurrentMode = ApplicationMode.Resolving;
		_SetPlatformActive();
	}

	/// <summary>
	/// Callback indicating that the Cloud Anchor was instantiated and the host request was
	/// made.
	/// </summary>
	/// <param name="isHost">Indicates whether this player is the host.</param>
	public void OnAnchorInstantiated(bool isHost)
	{
		if (m_AnchorAlreadyInstantiated)
		{
			return;
		}

		m_AnchorAlreadyInstantiated = true;
		networkManagerUIController.OnAnchorInstantiated(isHost);
	}

	/// <summary>
	/// Callback indicating that the Cloud Anchor was hosted.
	/// </summary>
	/// <param name="success">If set to <c>true</c> indicates the Cloud Anchor was hosted
	/// successfully.</param>
	/// <param name="response">The response string received.</param>
	public void OnAnchorHosted(bool success, string response)
	{
		m_AnchorFinishedHosting = success;
		networkManagerUIController.OnAnchorHosted(success, response);
	}

	/// <summary>
	/// Callback indicating that the Cloud Anchor was resolved.
	/// </summary>
	/// <param name="success">If set to <c>true</c> indicates the Cloud Anchor was resolved
	/// successfully.</param>
	/// <param name="response">The response string received.</param>
	public void OnAnchorResolved(bool success, string response)
	{
		networkManagerUIController.OnAnchorResolved(success, response);
	}

	/// <summary>
	/// Callback that happens when the client successfully connected to the server.
	/// </summary>
	private void _OnConnectedToServer()
	{
		if (m_CurrentMode == ApplicationMode.Hosting)
		{
			networkManagerUIController.ShowDebugMessage("Find a plane, tap to create a Cloud Anchor.");
		}
		else if (m_CurrentMode == ApplicationMode.Resolving)
		{
			networkManagerUIController.ShowDebugMessage("Waiting for Cloud Anchor to be hosted...");
		}
		else
		{
			_QuitWithReason("Connected to server with neither Hosting nor Resolving mode. " +
							"Please start the app again.");
		}
	}

	/// <summary>
	/// Callback that happens when the client disconnected from the server.
	/// </summary>
	private void _OnDisconnectedFromServer()
	{
		_QuitWithReason("Network session disconnected! " +
			"Please start the app again and try another room.");
	}

	/// <summary>
	/// Instantiates the anchor object at the pose of the m_LastPlacedAnchor Anchor. This will
	/// host the Cloud Anchor.
	/// </summary>
	private void _InstantiateAnchor()
	{
		// The anchor will be spawned by the host, so no networking Command is needed.
		GameObject.Find("LocalPlayer").GetComponent<LocalPlayerController>()
			.SpawnAnchor(Vector3.zero, Quaternion.identity, m_WorldOriginAnchor);
	}

	/// <summary>
	/// Instantiates a star object that will be synchronized over the network to other clients.
	/// </summary>
	private void _InstantiateStar()
	{
		// Star must be spawned in the server so a networking Command is used.
		GameObject.Find("LocalPlayer").GetComponent<LocalPlayerController>()
			.CmdSpawnStar(m_LastHitPose.Value.position, m_LastHitPose.Value.rotation);
	}

	/// <summary>
	/// Sets the corresponding platform active.
	/// </summary>
	private void _SetPlatformActive()
	{
		if (Application.platform != RuntimePlatform.IPhonePlayer)
		{
			arCoreRootGO.SetActive(true);
			arKitRootGO.SetActive(false);
		}
		else
		{
			arCoreRootGO.SetActive(false);
			arKitRootGO.SetActive(true);
		}
	}

	/// <summary>
	/// Indicates whether a star can be placed.
	/// </summary>
	/// <returns><c>true</c>, if stars can be placed, <c>false</c> otherwise.</returns>
	private bool _CanPlaceStars()
	{
		if (m_CurrentMode == ApplicationMode.Resolving)
		{
			return m_IsOriginPlaced;
		}

		if (m_CurrentMode == ApplicationMode.Hosting)
		{
			return m_IsOriginPlaced && m_AnchorFinishedHosting;
		}

		return false;
	}

	/// <summary>
	/// Resets the internal status.
	/// </summary>
	private void _ResetStatus()
	{
		// Reset internal status.
		m_CurrentMode = ApplicationMode.Ready;
		if (m_WorldOriginAnchor != null)
		{
			Destroy(m_WorldOriginAnchor.gameObject);
		}

		m_WorldOriginAnchor = null;
	}

	/// <summary>
	/// Check and update the application lifecycle.
	/// </summary>
	private void _UpdateApplicationLifecycle()
	{
		// Exit the app when the 'back' button is pressed.
		if (Input.GetKey(KeyCode.Escape))
		{
			Application.Quit();
		}

		var sleepTimeout = SleepTimeout.NeverSleep;

		#if !UNITY_IOS
			// Only allow the screen to sleep when not tracking.
			if (Session.Status != SessionStatus.Tracking)
			{
				const int lostTrackingSleepTimeout = 15;
				sleepTimeout = lostTrackingSleepTimeout;
			}
		#endif

		Screen.sleepTimeout = sleepTimeout;

		if (m_IsQuitting)
		{
			return;
		}

		// Quit if ARCore was unable to connect.
		if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
		{
			_QuitWithReason("Camera permission is needed to run this application.");
		}
		else if (Session.Status.IsError())
		{
			_QuitWithReason("ARCore encountered a problem connecting. " +
				"Please start the app again.");
		}
	}

	/// <summary>
	/// Quits the application after 5 seconds for the toast to appear.
	/// </summary>
	/// <param name="reason">The reason of quitting the application.</param>
	private void _QuitWithReason(string reason)
	{
		if (m_IsQuitting)
		{
			return;
		}

		networkManagerUIController.ShowDebugMessage(reason);
		m_IsQuitting = true;
		Invoke("_DoQuit", 5.0f);
	}

	/// <summary>
	/// Actually quit the application.
	/// </summary>
	private void _DoQuit()
	{
		Application.Quit();
	}
}
