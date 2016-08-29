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
	private Transform parentRig;
	[SerializeField]
	private float distanceAway;
	[SerializeField]
	private float distanceAwayMultiplier = 1.5f;
	[SerializeField]
	private float distanceUp;
	[SerializeField]
	private float distanceUpMultiplier = 5f;
	//[SerializeField]
	//private float smooth;
	[SerializeField]
	private CharacterControllerLogic follow;
	[SerializeField]
	private Transform followXform;
	[SerializeField]
	private float widescreen = 0.2f;
	[SerializeField]
	private float targetingTime = 0.5f;
	[SerializeField]
	private float firstPersonThreshold = 0.5f;
	[SerializeField]
	private float firstPersonLookSpeed = 3.0f;
	[SerializeField]
	private Vector2 firstPersonXAxisClamp = new Vector2(-70.0f, 90.0f);
	[SerializeField]
	private float fPSRotationDegreePerSecond = 120f;
	[SerializeField]
	private float freeThreshold = -0.1f;
	[SerializeField]
	private Vector2 camMindDistFromChar = new Vector2(1f, -0.5f);
	[SerializeField]
	private float rightStickThreshold = 0.1f;
	[SerializeField]
	private const float freeRotationDegreePerSecond = -5f;

	//Smoothing and damping
	private Vector3 velocityCamSmooth = Vector3.zero;
	[SerializeField]
	private float camSmoothDampTime = 0.1f;
	private Vector3 velocityLookDir = Vector3.zero;
	[SerializeField]
	private float lookDirDampTime = 0.1f;

	//Private global only
	private Vector3 lookDir;
	private Vector3 curLookDir;
	private Vector3 targetPosition;
	private BarsEffect barEffect;
	private CamStates camState = CamStates.Behind;
	private float xAxisRot = 0.0f;
	private CameraPosition firstPersonCamPos;
	private float lookWeight;
	private const float TARGETING_THRESHOLD = 0.01f;
	private Vector3 savedRigToGoal;
	private float distanceAwayFree;
	private float distanceUpFree;
	private Vector2 rightStickPrevFrame = Vector2.zero;
	private float rightX;
	private float rightY;
	private float leftX;
	private float leftY;

	#endregion

	#region Properties (public)

	public Transform ParentRig
	{
		get
		{
			return this.parentRig;
		}
	}



	public CamStates CamState {
		get 
		{
			return this.camState;
		}
	}

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

		parentRig = this.transform.parent;
		if (parentRig == null) {
			Debug.LogError ("Parent camera to empty game object for cameraRig", this);
		}

		follow = GameObject.FindWithTag("Player").GetComponent<CharacterControllerLogic>();
		followXform = GameObject.FindWithTag ("Player").transform;

		lookDir = followXform.forward;
		curLookDir = followXform.forward;


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

		Debug.Log(rightX);
		
		Vector3 characterOffset = followXform.position + new Vector3 (0f, distanceUp, 0f);
		Vector3 lookAt = characterOffset;
		Vector3 targetPosition = Vector3.zero;


		//Determine camera state
		if (Input.GetAxis ("Target") > TARGETING_THRESHOLD) {
			barEffect.coverage = Mathf.SmoothStep (barEffect.coverage, widescreen, targetingTime);

			camState = CamStates.Target;
		} else {
			barEffect.coverage = Mathf.SmoothStep (barEffect.coverage, 0f, targetingTime);

			// * First Person *
			if (rightY > firstPersonThreshold && camState != CamStates.Free && !follow.IsInLocomotion ()) {
				//Reset look before entering the first person mode
				xAxisRot = 0;
				lookWeight = 0f;
				camState = CamStates.FirstPerson;
			}

			// * Free Camera *
			if (rightY < freeThreshold && System.Math.Round (follow.Speed, 2) == 0) {
				camState = CamStates.Free;
				savedRigToGoal = Vector3.zero;
			}

			// * Behind the back *
			if ((camState == CamStates.FirstPerson && Input.GetButton ("ExitFPV")) ||
			    (camState == CamStates.Target && (Input.GetAxis ("Target") <= TARGETING_THRESHOLD))) {
				camState = CamStates.Behind;
			}
		}


		//Set the look at weight - amount to use to look at IK vs using the head's animation ** Needs to be setup properly for UNITY5
		//follow.Animator.SetLookAtWeight(lookWeight);



		//Execute camera state
		switch (camState) {
		case CamStates.Behind:
			ResetCamera ();

				// Only update camera look direction if moving
			if (follow.Speed > follow.LocomotionThreshold && follow.IsInLocomotion ()) {
				lookDir = Vector3.Lerp (followXform.right * (leftX < 0 ? 1f : -1f), followXform.forward * (leftY < 0 ? -1f : 1f), Mathf.Abs (Vector3.Dot (this.transform.forward, followXform.forward)));
				Debug.DrawRay (this.transform.position, lookDir, Color.white);

				// Calculate direction from camera to player, kill Y, and normalize to give a calid direction with unit magnitude
				curLookDir = Vector3.Normalize (characterOffset - this.transform.position);
				curLookDir.y = 0;
				Debug.DrawRay (this.transform.position, curLookDir, Color.green);

				// Damping makes it so we don't update targetPosition while pivoting; camera shouldn't rotate around player
				curLookDir = Vector3.SmoothDamp (curLookDir, lookDir, ref velocityLookDir, lookDirDampTime);
			}

			targetPosition = characterOffset + followXform.up * distanceUp - Vector3.Normalize (curLookDir) * distanceAway;
			Debug.DrawLine (followXform.position, targetPosition, Color.magenta);

			break;
		case CamStates.Target:
			ResetCamera ();

			lookDir = followXform.forward;
			curLookDir = followXform.forward;
			targetPosition = characterOffset + followXform.up * distanceUp - lookDir * distanceAway;

			break;

		case CamStates.FirstPerson:

				//Looking up and down
				//Calculate the amount of rotation and apply to the firsPersonCamPos GameObject
			xAxisRot += (leftY * firstPersonLookSpeed);
			xAxisRot = Mathf.Clamp (xAxisRot, firstPersonXAxisClamp.x, firstPersonXAxisClamp.y);
			firstPersonCamPos.Xform.localRotation = Quaternion.Euler (xAxisRot, 0, 0);

				// Superimpose firstPersonCamPos GameObject's rotation on camera
			Quaternion rotationShift = Quaternion.FromToRotation (this.transform.forward, firstPersonCamPos.Xform.forward);
			this.transform.rotation = rotationShift * this.transform.rotation;

				// Move character model's head **Needs to be updated for UNITY 5
				//follow.Animator.SetLookAtPosition(firstPersonCamPos.Xform.position + firstPersonCamPos.Xform.forward);
				//lookWeight = Mathf.Lerp(lookWeight, 1.0f, Time.deltaTime * firstPersonLookSpeed);

				// Looking left and right
				//	Similarly to how character is rotated while in locomotion, use Quaternion * to add rotation to character
			Vector3 rotationAmount = Vector3.Lerp (Vector3.zero, new Vector3 (0f, fPSRotationDegreePerSecond * (leftX < 0f ? -1f : 1f), 0f), Mathf.Abs (leftX));
			Quaternion deltaRotation = Quaternion.Euler (rotationAmount * Time.deltaTime);
			follow.transform.rotation = (follow.transform.rotation * deltaRotation);

				// Move camera to first person position
			targetPosition = firstPersonCamPos.Xform.position;

				// Smoothly transition look direction towards firstPersonCamPos when entering first person mode
			lookAt = Vector3.Lerp (targetPosition + followXform.forward, this.transform.position + this.transform.forward, camSmoothDampTime * Time.deltaTime);
			Debug.DrawRay (Vector3.zero, lookAt, Color.black);
			Debug.DrawRay (Vector3.zero, targetPosition + followXform.forward, Color.white);
			Debug.DrawRay (Vector3.zero, firstPersonCamPos.Xform.position + firstPersonCamPos.Xform.forward, Color.cyan);


				// Choose look at target based on distance
			lookAt = (Vector3.Lerp (this.transform.position + this.transform.forward, lookAt, Vector3.Distance (this.transform.position, firstPersonCamPos.Xform.position)));
			break;

		case CamStates.Free:
			lookWeight = Mathf.Lerp (lookWeight, 0.0f, Time.deltaTime * firstPersonLookSpeed);

				// Move height and distance from character in separate parentRig transform since RoatateAround has control of both postion and rotation
			Vector3 rigToGoalDirection = Vector3.Normalize (characterOffset - this.transform.position);
				// Can't calculate distanceAway from a vector with Y axis rotation in it; zero it out
			rigToGoalDirection.y = 0f;

			Vector3 rigToGoal = characterOffset - parentRig.position;
			rigToGoal.y = 0;
			Debug.DrawRay (parentRig.transform.position, rigToGoal, Color.red);

				//Moving camera in and out
				//If statement works for positive values; don't tween if stick not increasing in either direction;
				// also don't tween if user is rotating. Checked agains RIGHT X THRESHOLD because very small values for rightY mess up the Lerp function
			if (rightY < -1f * rightStickThreshold && rightY <= rightStickPrevFrame.y && Mathf.Abs (rightX) < rightStickThreshold) {
				distanceUpFree = Mathf.Lerp (distanceUp, distanceUp * distanceUpMultiplier, Mathf.Abs (rightY));
				distanceAwayFree = Mathf.Lerp (distanceAway, distanceAway * distanceAwayMultiplier, Mathf.Abs (rightY));
				targetPosition = characterOffset + followXform.up * distanceUpFree - rigToGoalDirection * distanceAwayFree;
			} else if (rightY > rightStickThreshold && rightY >= rightStickPrevFrame.y && Mathf.Abs (rightX) < rightStickThreshold) {
				//Subtract height of camera from height of player to find Y distance
				distanceUpFree = Mathf.Lerp (Mathf.Abs (transform.position.y - characterOffset.y), camMindDistFromChar.y, rightY);
				//Use magnitude function to find X distance
				distanceAwayFree = Mathf.Lerp (rigToGoal.magnitude, camMindDistFromChar.x, rightY);

				targetPosition = characterOffset + followXform.up * distanceUpFree - rigToGoalDirection * distanceAwayFree;
			}

			//Store direction onlu if right stick inactive
			if (rightX != 0 || rightY != 0) {
				savedRigToGoal = rigToGoalDirection;
			}

			//Rotating Around
			parentRig.RotateAround(characterOffset, followXform.up, freeRotationDegreePerSecond * (Mathf.Abs(rightX) > rightStickThreshold ? rightX : 0f));

			// Still need to track camera behind player even if they aren't using the right stick; achive this by saving distanceAwayFree every frame
			if (targetPosition == Vector3.zero)
			{
				targetPosition = characterOffset + followXform.up * distanceUpFree - savedRigToGoal * distanceAwayFree;
			}

			//SmoothPosition(transform.position, targetPosition);
			//transform.LookAt(lookAt);

			break;
		}

		//if (camState != CamStates.Free) 
		//{
			CompensateForWalls (characterOffset, ref targetPosition);

			SmoothPosition (this.transform.position, targetPosition);

			//make sure the camera is looking the right way!
			transform.LookAt (lookAt);
		//}

		rightStickPrevFrame = new Vector2(rightX, rightY);

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

	/// <summary>
	/// Reset local position of camera inside of parentRig and resets character's look IK ** Needs to be Updated for UNITY 5
	private void ResetCamera ()
	{
		lookWeight = Mathf.Lerp (lookWeight, 0.0f, Time.deltaTime * firstPersonLookSpeed);
		transform.localRotation = Quaternion.Lerp (transform.localRotation, Quaternion.identity, Time.deltaTime);
	}

	#endregion
}
