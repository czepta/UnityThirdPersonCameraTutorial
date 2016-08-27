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


	//Private global only
	private float speed = 0.0f;
	private float direction = 0f;
	private float horizontal = 0.0f;
	private float vertical = 0.0f;
	private AnimatorStateInfo stateInfo;

	//Hashes
	private int m_LocomotionId = 0;

	#endregion

	#region Properties (public)

	#endregion


	#region Unity event functions

	/// <summary>
	/// Use this for initialization.
	/// </summary>
	void Start () 
	{
		animator = GetComponent<Animator> ();

		if (animator.layerCount >= 2) 
		{
			animator.SetLayerWeight(1, 1);
		}

		//Hash all animation names for performance
		m_LocomotionId = Animator.StringToHash("Base Layer.Locomotion");

	}

	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update ()
	{
		if (animator) 
		{
			stateInfo = animator.GetCurrentAnimatorStateInfo(0);

			//Pull values from controller/keyboard
			horizontal = Input.GetAxis ("Horizontal");
			vertical = Input.GetAxis ("Vertical");

			//Translate the control's stick coordinates into world/camera/character space
			StickToWorldSpace(this.transform, gamecam.transform, ref direction, ref speed);


			animator.SetFloat ("Speed", speed);
			animator.SetFloat ("Direction", direction, directionDampTime, Time.deltaTime);
		}
	}

	/// <summary>
	/// Any code that moves the character needs to be checked against physics
	/// </summary>
	void FixedUpdate ()
	{
		//Rotate character model if stick is tilted right or left, but only if character is moving in that direction
		if (IsInLocomotion () && ((direction >= 0 && horizontal >= 0) || (direction < 0 && horizontal < 0))) {
			Vector3 rotationAmount = Vector3.Lerp (Vector3.zero, new Vector3 (0f, rotationDegreesPerSecond * (horizontal < 0f ? -1f : 1f), 0f), Mathf.Abs(horizontal));
			Quaternion deltaRotation = Quaternion.Euler (rotationAmount * Time.deltaTime);
			this.transform.rotation = (this.transform.rotation * deltaRotation);
		}
	}

	/// <summary>
	/// Debugging information should be put here.
	/// </summary>
	void onDrawGizmos () {

	}

	#endregion

	#region Methods

	public void StickToWorldSpace(Transform root, Transform camera,  ref float directionOut, ref float speedOut) 
	{
		Vector3 rootDirection = root.forward;

		Vector3 stickDirection = new Vector3(horizontal, 0, vertical);

		speedOut = stickDirection.sqrMagnitude;

		//Get Camera rotation
		Vector3 CameraDirection = camera.forward;
		CameraDirection.y = 0.0f; //Kill Y
		Quaternion referentialShift = Quaternion.FromToRotation(Vector3.forward, CameraDirection);

		//Convert joystick input in Worldspace coordinates
		Vector3 moveDirection = referentialShift * stickDirection;
		Vector3 axisSign = Vector3.Cross(moveDirection, rootDirection);

		//Debug.DrawRay(new Vector3(root.position.x, root.position.y + 2f, root.position.z), moveDirection, Color.green);
		//Debug.DrawRay(new Vector3(root.position.x, root.position.y + 2f, root.position.z), rootDirection, Color.magenta);
		Debug.DrawRay(new Vector3(root.position.x, root.position.y + 2f, root.position.z), stickDirection, Color.blue);

		float angleRootToMove = Vector3.Angle(rootDirection, moveDirection) * (axisSign.y >= 0 ? -1f : 1f);

		angleRootToMove /= 180f;

		directionOut = angleRootToMove * directionSpeed;
	}

	public bool IsInLocomotion()
	{
		return stateInfo.fullPathHash == m_LocomotionId;
	}


	#endregion
}
