using UnityEngine;
using System.Collections;

/// <summary>
/// Struct to hold data for aligning camera
///</summary>
struct CameraPosition
{
	//Position to align camera to, probably somewhere behind the character
	//or position to point camera at, probably somewhere along character's axis
	private Vector3 position;

	//Transform used for any rotation
	private Transform xForm;

	public Vector3 Position { get { return position; } set { position = value; } }
	public Transform Xform { get { return xForm; } set { xForm = value; } }

	public void Init (string camName, Vector3 pos, Transform Transform, Transform parent)
	{
		position = pos;
		xForm = Transform;
		xForm.name = camName;
		xForm.parent = parent;
		xForm.localPosition = Vector3.zero;
		xForm.localPosition = position;
	}
}

/// <summary>
/// #DESCRIPTION OF CLASS#
/// </summary>

[RequireComponent (typeof (BarsEffect))] 

public class ThirdPersonCamera : MonoBehaviour {

	#region Variables (private)

	//Inspector Serialized
	[SerializeField]
	private float distanceAway;
	[SerializeField]
	private float distanceUp;
	[SerializeField]
	private float smooth;
	[SerializeField]
	private Transform followXform;
	[SerializeField]
	private CharacterControllerLogic follow;
	[SerializeField]
	private float widescreen = 0.2f;
	[SerializeField]
	private float targetingTime = 0.5f;
	[SerializeField]
	private float firstPersonThreshold = 0.5f;

	//Smoothing and damping
	private Vector3 velocityCamSmooth = Vector3.zero;
	[SerializeField]
	private float camSmoothDampTime = 0.1f;

	//Private global only
	private Vector3 lookDir;
	private Vector3 targetPosition;
	private BarsEffect barEffect;
	private CamStates camState = CamStates.Behind;
	private float xAxisRot = 0.0f;
	private CameraPosition firstPersonCamPos;
	private float lookWeight;
	private const float TARGETING_THRESHOLD = 0.01f;

	#endregion

	#region Properties (public)

	public enum CamStates
	{
		Behind,
		FirstPerson,
		Target,
		Free
	}

	#endregion


	#region Unity event functions

	/// <summary>
	/// Use this for initialization.
	/// </summary>
	void Start ()
	{
		follow = GameObject.FindWithTag("Player").GetComponent<CharacterControllerLogic>();
		
		followXform = GameObject.FindWithTag ("Player").transform;
		lookDir = followXform.forward;

		barEffect = GetComponent<BarsEffect> ();
		if (barEffect == null) 
		{
			Debug.LogError ("Attach a widescreen BarsEffect script to the camera.", this);
		}

		//Position and parent a GameObject where first person view should be
		firstPersonCamPos = new CameraPosition();
		firstPersonCamPos.Init
			(
				"First Person Camera",
				new Vector3(0.0f, 1.6f, 0.2f),
				new GameObject().transform,
				followXform
			);



	}

	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update () {

	}

	/// <summary>
	/// Debugging information should be put here.
	/// </summary>
	void onDrawGizmos () {

	}

	void LateUpdate ()
	{
		//Pull values from the controller/keyboard
		float rightX = Input.GetAxis ("RightStickX");
		float rightY = Input.GetAxis ("RightStickY");
		float leftX = Input.GetAxis ("Horizontal");
		float leftY = Input.GetAxis ("Vertical");
		
		Vector3 characterOffset = followXform.position + new Vector3 (0f, distanceUp, 0f);

		//Determine camera state
		if (Input.GetAxis ("Target") > TARGETING_THRESHOLD) {
			barEffect.coverage = Mathf.SmoothStep (barEffect.coverage, widescreen, targetingTime);

			camState = CamStates.Target;
		} else {
			barEffect.coverage = Mathf.SmoothStep (barEffect.coverage, 0f, targetingTime);

			// * First Person *
			if (rightY > firstPersonThreshold && !follow.IsInLocomotion ()) {
				//Reset look before entering the first person mode
				xAxisRot = 0;
				lookWeight = 0f;
				camState = CamStates.FirstPerson;
			}

			// * Behind the back *
			if ((camState == CamStates.FirstPerson && Input.GetButton ("ExitFPV")) ||
			    (camState == CamStates.Target && (Input.GetAxis ("Target") <= TARGETING_THRESHOLD))) 
			{
				camState = CamStates.Behind;
			}
		}

		//Execute camera state
		switch (camState)
		{
			case CamStates.Behind:

				//Calculate direction from camera to player, kill y, and normalize to give a valid direction with unit magnitude
				lookDir = characterOffset - this.transform.position;
				lookDir.y = 0 ;
				lookDir.Normalize();
				Debug.DrawRay(this.transform.position, lookDir, Color.green);

				//setting the target position to be the correct offset from the player
				targetPosition = characterOffset + followXform.up * distanceUp - lookDir * distanceAway;
				Debug.DrawRay(followXform.position, Vector3.up * distanceUp, Color.red);
				Debug.DrawRay(followXform.position, -1f * followXform.forward * distanceAway, Color.blue);
				Debug.DrawLine(followXform.position, targetPosition, Color.magenta);

				break;

			case CamStates.Target:

				lookDir = followXform.forward;

				break;

			case CamStates.FirstPerson:
				Debug.Log("In first person", this);
				break;
		}

		targetPosition = characterOffset + followXform.up * distanceUp - lookDir * distanceAway;

		CompensateForWalls(characterOffset, ref targetPosition);

		SmoothPosition(this.transform.position, targetPosition);


		//make sure the camera is looking the right way!
		transform.LookAt(followXform);
	}



	#endregion

	#region Methods

	private void SmoothPosition (Vector3 fromPos, Vector3 toPos)
	{

		//making a smooth transition between camera's current position and the  position it wants to be in
		this.transform.position = Vector3.SmoothDamp(fromPos, toPos, ref velocityCamSmooth, camSmoothDampTime);

	}

	private void CompensateForWalls (Vector3 fromObject, ref Vector3 toTarget)
	{
		Debug.DrawLine (fromObject, toTarget, Color.cyan);
		// Compensate for walls between camera
		RaycastHit wallHit = new RaycastHit ();
		if (Physics.Linecast (fromObject, toTarget, out wallHit))
		{
			Debug.DrawRay (wallHit.point, Vector3.left, Color.red);
			toTarget = new Vector3 (wallHit.point.x, toTarget.y, wallHit.point.z);
		}
	}

	#endregion
}
