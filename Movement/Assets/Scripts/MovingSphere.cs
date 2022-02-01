using UnityEngine;

class MovingSphere : MonoBehaviour {

	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f, maxAcceleration = 10f, maxAirAcceleration = 1f, maxSnapSpeed = 100f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 2f;

	[SerializeField, Range(0, 5)]
	int maxAirJumps = 0;

	[SerializeField]
	bool wallJumpsEnabled = true;

	[SerializeField, Range(0f, 90f)]
	float maxGroundAngle = 40f, maxStairsAngle = 50f;

	[SerializeField, Min(0f)]
	float probeDistance = 1f;

	[SerializeField]
	LayerMask probeMask = -1, stairsMask = -1;

	[SerializeField]
	Transform playerInputSpace = default;

	Vector3 velocity, desiredVelocity, connectionVelocity;

	Vector3 contactNormal, steepNormal;

	Vector3 upAxis, rightAxis, forwardAxis;

	int groundContactCount, steepContactCount;
	int stepsSinceLastGrounded, stepsSinceLastJump;

	bool OnGround => groundContactCount > 0;
	bool OnSteep => steepContactCount > 0;

	bool desiredJump;
	int jumpPhase;

	Rigidbody body, connectedBody, previousConnectedBody;

	Vector3 connectionWorldPosition, connectionLocalPosition;

	float minGroundDotProduct, minStairsDotProduct;

	void OnValidate () {
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
	}

	void Awake () {
		body = GetComponent<Rigidbody>();
		body.useGravity = false;
		OnValidate();
	}

	void Update () {
		Vector2 playerInput;
		playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
		playerInput = Vector2.ClampMagnitude(playerInput, 1f);

		if (playerInputSpace) {
			forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
			rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
		}
		else {
			forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
			rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
		}

		desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

		desiredJump |= Input.GetButtonDown("Jump"); // Will only change to true -- equivalent to desiredJump = desiredJump || Input.Etcetera

		if (connectedBody) {
			if (connectedBody.isKinematic || connectedBody.mass >= body.mass) {
				UpdateConnectionState();
			}
		}

		GetComponent<Renderer>().material.SetColor(
			"_Color", Color.white * (OnGround || OnSteep ? 0f : 1f)
		); // Debug coloration
	}

	void FixedUpdate () {
		Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
		// Debug.Log(velocity.magnitude);
		UpdateState();
		AdjustVelocity();
		
		if (desiredJump) {
			desiredJump = false;
			Jump(gravity);
		}

		velocity += gravity * Time.deltaTime;

		body.velocity = velocity;
		ClearState();
	}

	void ClearState () {
		groundContactCount = steepContactCount = 0;
		contactNormal = steepNormal = connectionVelocity = Vector3.zero;
		previousConnectedBody = connectedBody;
		connectedBody = null;
	}

	void UpdateState () {
		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		velocity = body.velocity;
		if (OnGround || SnapToGround() || CheckSteepContacts()) {
			stepsSinceLastGrounded = 0;
			if (stepsSinceLastJump > 1) {
				jumpPhase = 0;
			}
			if (groundContactCount > 1) {
				contactNormal.Normalize();
			}
		}
		else {
			contactNormal = upAxis;
		}
	}

	void UpdateConnectionState () {
		if (connectedBody == previousConnectedBody) {
			Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
			connectionVelocity = connectionMovement / Time.deltaTime;
		}
		connectionWorldPosition = body.position;
		connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition); // Transforms point from world space to local space
	}

	void Jump (Vector3 gravity) {
		Vector3 jumpDirection;

		if (OnGround) {
			jumpDirection = contactNormal;
		}
		else if (OnSteep && wallJumpsEnabled) {
			jumpDirection = steepNormal;
			jumpPhase = 0;
		}
		else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) {
			if (jumpPhase == 0) {
				jumpPhase = 1;
			}
			jumpDirection = contactNormal;
		}
		else {
			return;
		}


		stepsSinceLastJump = 0;
		jumpPhase += 1;
		float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);

		jumpDirection = (jumpDirection + upAxis).normalized; // Bias jumps so gravity doesn't make wall-jumping to gain height impossible
		float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
		
		if (alignedSpeed > 0f) { // Are we moving away from the contact surface?
			jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
		}
		
		velocity += jumpDirection * jumpSpeed;
	}

	void AdjustVelocity () {
		Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal).normalized;
		Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal).normalized;

		Vector3 relativeVelocity = velocity - connectionVelocity;

		float currentX = Vector3.Dot(relativeVelocity, xAxis); // Length of x axis is always one, so no need to divide! Just projects current velocity onto new axis
		float currentZ = Vector3.Dot(relativeVelocity, zAxis);

		float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
		float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

		velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
	}

	bool SnapToGround () {
		if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
			return false;
		}
		float speed = velocity.magnitude;
		if (speed > maxSnapSpeed) {
			return false;
		}
		if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask)) {
			print("Raycast failed");
			return false;
		}
		float upDot = Vector3.Dot(upAxis, hit.normal);
		if (upDot < GetMinDot(hit.collider.gameObject.layer)) {
			return false;
		}

		groundContactCount = 1;
		contactNormal = hit.normal;
		float dot = Vector3.Dot(velocity, hit.normal);
		if (dot > 0f) {
			velocity = (velocity - hit.normal * dot).normalized * speed;
		}

		connectedBody = hit.rigidbody;

		//Debug.Log("Snapping to ground");

		return true;
	}

	bool CheckSteepContacts () {
		if (steepContactCount > 1) {
			steepNormal.Normalize();
			float upDot = Vector3.Dot(upAxis, steepNormal);
			if (upDot >= minGroundDotProduct) {
				groundContactCount = 1;
				contactNormal = steepNormal;
				return true;
			}
		}
		return false;
	}

	float GetMinDot (int layer) {
		return (stairsMask & (1 << layer)) == 0 ? minGroundDotProduct : minStairsDotProduct; // Bitwise logical AND returns binary sequence with 1s where there are 1s in both inputs
																							 // Ex. 101100100 stair mask compared with layer 000000100 returns 1 because there is a 1 both in the layer "binary" and the bitmask in the same position
	}

	void OnCollisionEnter (Collision collision) {
		EvaluateCollision(collision);
	}

	void OnCollisionStay (Collision collision) {
		EvaluateCollision(collision);
	}

	void EvaluateCollision (Collision collision) {
		for (int i = 0; i < collision.contactCount; i++) {
			float minDot = GetMinDot(collision.gameObject.layer);
			Vector3 normal = collision.GetContact(i).normal;
			float upDot = Vector3.Dot(upAxis, normal);
			if (upDot >= minDot) {
				groundContactCount += 1;
				contactNormal += normal;
				connectedBody = collision.rigidbody;
			}
			else if (upDot > -0.01f) {
				steepContactCount += 1;
				steepNormal += normal;
				if (groundContactCount == 0) {
					connectedBody = collision.rigidbody;
				}
			}
		}
	}

	Vector3 ProjectDirectionOnPlane (Vector3 direction, Vector3 normal) {
		return (direction - normal * Vector3.Dot(direction, normal)).normalized;
	}
}