using UnityEngine;
using System.Collections;

/// <summary>
/// #DESCRIPTION OF CLASS#
/// </summary>
public class CharacterControllerLogic : MonoBehaviour {

	#region Variables (private)

	//Inspector serialized
	[SerializeField]
	private Animator animator;
	[SerializeField]
	private float directionDampTime = .25f;
	[SerializeField]
	private ThirdPersonCamera gamecam;
	[SerializeField]
	private float directionSpeed = 3.0f;
	[SerializeField]
	private float rotationDegreesPerSecond = 120f;
	[SerializeField]
	private float speedDampTime = 0.05f;
	[SerializeField]
	private float fovDampTime = 3f;
	[SerializeField]
	private float jumpMultiplier = 1f;
	[SerializeField]
	private CapsuleCollider capCollider;
	[SerializeField]
	private float jumpDist = 1f;


	//Private global only
	private float speed = 0.0f;
	private float direction = 0f;
	private float leftX = 0.0f;
	private float leftY = 0.0f;
	private AnimatorStateInfo stateInfo;
	private AnimatorTransitionInfo transInfo;
	private float charAngle = 0f;
	private const float SPRINT_SPEED = 2.0f;
	private const float SPRINT_FOV = 75.0f;
	private const float NORMAL_FOV = 60.0f;
	private float capsuleHeight;

	//Hashes
	private int m_LocomotionId = 0;
	private int m_LocomotionPivotLId = 0;
	private int m_LocomotionPivotRId = 0;
	private int m_LocomotionPivotLTransId = 0;
	private int m_LocomotionPivotRTransId = 0;

	#endregion

	#region Properties (public)

	public Animator Animator {
		get {
			return this.animator;
		}
	}

	public float Speed {
		get {
			return this.speed;
		}
	}

	public float LocomotionThreshold { get { return 0.2f; } }

	#endregion


	#region Unity event functions

	/// <summary>
	/// Use this for initialization.
	/// </summary>
	void Start () 
	{
		animator = GetComponent<Animator> ();
		capCollider = GetComponent<CapsuleCollider>();
		capsuleHeight = capCollider.height;

		if (animator.layerCount >= 2) 
		{
			animator.SetLayerWeight(1, 1);
		}

		//Hash all animation names for performance
		m_LocomotionId = Animator.StringToHash("Base Layer.Locomotion");
		m_LocomotionPivotLId = Animator.StringToHash("Base Layer.LocomotionPivotL");
		m_LocomotionPivotRId = Animator.StringToHash("Base Layer.LocomotionPivotR");
		m_LocomotionPivotLTransId = Animator.StringToHash("Base Layer.Locomotion -> Base Layer.LocomotionPivotL");
		m_LocomotionPivotRTransId = Animator.StringToHash("Base Layer.Locomotion -> Base Layer.LocomotionPivotR");

	}

	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update ()
	{
		if (animator && gamecam.CamState != ThirdPersonCamera.CamStates.FirstPerson) {
			stateInfo = animator.GetCurrentAnimatorStateInfo (0);
			transInfo = animator.GetAnimatorTransitionInfo (0);

			// Press A to Jump
			if (Input.GetButton ("Jump")) {
				animator.SetBool ("Jump", true);
			} else {
				animator.SetBool ("Jump", false);
			}

			//Pull values from controller/keyboard
			leftX = Input.GetAxis ("Horizontal");
			leftY = Input.GetAxis ("Vertical");

			charAngle = 0f;
			direction = 0f;
			float charSpeed = 0f;

			//Translate the control's stick coordinates into world/camera/character space
			StickToWorldSpace (this.transform, gamecam.transform, ref direction, ref charSpeed, ref charAngle, IsInPivot ());

			//Press B to Sprint
			if (Input.GetButton ("Sprint")) {
				speed = Mathf.Lerp (speed, SPRINT_SPEED, Time.deltaTime);
				gamecam.GetComponent<Camera> ().fieldOfView = Mathf.Lerp (gamecam.GetComponent<Camera> ().fieldOfView, SPRINT_FOV, fovDampTime * Time.deltaTime);
			} else {
				speed = charSpeed;
				gamecam.GetComponent<Camera> ().fieldOfView = Mathf.Lerp (gamecam.GetComponent<Camera> ().fieldOfView, NORMAL_FOV, fovDampTime * Time.deltaTime);
			}
			if (!Input.GetButton ("Sprint")) {
				animator.SetFloat ("Speed", speed, speedDampTime, Time.deltaTime);
			}

			animator.SetFloat ("Speed", speed, speedDampTime, Time.deltaTime);
			animator.SetFloat ("Direction", direction, directionDampTime, Time.deltaTime);


			if (speed > LocomotionThreshold) { // Dead Zone
				if (!IsInPivot ()) 
				{
					Animator.SetFloat("Angle", charAngle);
				}
			}

			if (speed < LocomotionThreshold && Mathf.Abs (leftX) < 0.05f) // Dead Zone
			{
				animator.SetFloat("Direction", 0f);
				animator.SetFloat("Angle", 0f);	
			}
		}
	}

	/// <summary>
	/// Any code that moves the character needs to be checked against physics
	/// </summary>
	void FixedUpdate ()
	{
		//Rotate character model if stick is tilted right or left, but only if character is moving in that direction
		if (IsInLocomotion () && gamecam.CamState != ThirdPersonCamera.CamStates.Free && !IsInPivot () && ((direction >= 0 && leftX >= 0) || (direction < 0 && leftX < 0))) {
			Vector3 rotationAmount = Vector3.Lerp (Vector3.zero, new Vector3 (0f, rotationDegreesPerSecond * (leftX < 0f ? -1f : 1f), 0f), Mathf.Abs (leftX));
			Quaternion deltaRotation = Quaternion.Euler (rotationAmount * Time.deltaTime);
			this.transform.rotation = (this.transform.rotation * deltaRotation);

		}

		if (IsInJump ()) {
			
			float oldY = transform.position.y;
				transform.Translate (Vector3.up * jumpMultiplier * animator.GetFloat ("JumpCurve"));
			if (IsInLocomotionJump ()) {
				transform.Translate (Vector3.forward * Time.deltaTime * jumpDist);
			}
			capCollider.height = capsuleHeight + (animator.GetFloat ("CapsuleCurve") * 0.5f);
			if (gamecam.CamState != ThirdPersonCamera.CamStates.Free) {
				gamecam.ParentRig.Translate(Vector3.up * (transform.position.y - oldY ));
			}
		}
	}

	/// <summary>
	/// Debugging information should be put here.
	/// </summary>
	void onDrawGizmos () {

	}

	#endregion

	#region Methods

	public bool IsInJump ()
	{
		return (IsInIdleJump() || IsInLocomotionJump());
	}

	public bool IsInIdleJump ()
	{
		return animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.IdleJump");
	}

	public bool IsInLocomotionJump ()
	{
		return animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.LocomotionJump");
	}

	public bool IsInPivot ()
	{
		return stateInfo.fullPathHash == m_LocomotionPivotLId ||
			stateInfo.fullPathHash == m_LocomotionPivotRId ||
			transInfo.fullPathHash == m_LocomotionPivotLTransId ||
			transInfo.fullPathHash == m_LocomotionPivotRTransId;
	}

	public bool IsInLocomotion()
	{
		return stateInfo.fullPathHash == m_LocomotionId;
	}


	public void StickToWorldSpace (Transform root, Transform camera, ref float directionOut, ref float speedOut, ref float angleOut, bool isPivoting)
	{
		Vector3 rootDirection = root.forward;

		Vector3 stickDirection = new Vector3 (leftX, 0, leftY);

		speedOut = stickDirection.sqrMagnitude;

		//Get Camera rotation
		Vector3 CameraDirection = camera.forward;
		CameraDirection.y = 0.0f; //Kill Y
		Quaternion referentialShift = Quaternion.FromToRotation (Vector3.forward, CameraDirection);

		//Convert joystick input in Worldspace coordinates
		Vector3 moveDirection = referentialShift * stickDirection;
		Vector3 axisSign = Vector3.Cross (moveDirection, rootDirection);

		//Debug.DrawRay(new Vector3(root.position.x, root.position.y + 2f, root.position.z), moveDirection, Color.green);
		//Debug.DrawRay(new Vector3(root.position.x, root.position.y + 2f, root.position.z), rootDirection, Color.magenta);
		Debug.DrawRay (new Vector3 (root.position.x, root.position.y + 2f, root.position.z), stickDirection, Color.blue);

		float angleRootToMove = Vector3.Angle (rootDirection, moveDirection) * (axisSign.y >= 0 ? -1f : 1f);

		if (!isPivoting) {
			angleOut = angleRootToMove;
		}

		angleRootToMove /= 180f;

		directionOut = angleRootToMove * directionSpeed;
	}
	#endregion
}
