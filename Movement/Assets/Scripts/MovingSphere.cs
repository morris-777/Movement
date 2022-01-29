using UnityEngine;

class MovingSphere : MonoBehaviour {

	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 10f, maxAcceleration = 10f, maxAirAcceleration = 1f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 2f;

	[SerializeField, Range(0, 5)]
	int maxAirJumps = 0;

	[SerializeField, Range(0f, 90f)]
	float maxGroundAngle = 40f;

	Vector3 velocity, desiredVelocity;

	Vector3 contactNormal;

	int groundContactCount;

	bool OnGround => groundContactCount > 0;

	bool desiredJump;
	int jumpPhase;

	Rigidbody body;

	float minGroundDotProduct;

	void OnValidate () {
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
	}

	void Awake () {
		body = GetComponent<Rigidbody>();
	}

	void Update () {
		Vector2 playerInput;
		playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");
		playerInput = Vector2.ClampMagnitude(playerInput, 1f);

		desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;

		desiredJump |= Input.GetButtonDown("Jump"); // Will only change to true

		GetComponent<Renderer>().material.SetColor(
			"_Color", Color.white * (groundContactCount * 0.25f)
		);
	}

	void FixedUpdate () {
		UpdateState();
		AdjustVelocity();
		
		if (desiredJump) {
			desiredJump = false;
			Jump();
		}

		body.velocity = velocity;
		ClearState();
	}

	void ClearState () {
		groundContactCount = 0;
		contactNormal = Vector3.zero;
	}

	void UpdateState () {
		velocity = body.velocity;
		if (OnGround) {
			jumpPhase = 0;
			if (groundContactCount > 1) {
				contactNormal.Normalize();
			}
		}
		else {
			contactNormal = Vector3.up;
		}
	}

	void Jump () {
		if (OnGround || jumpPhase < maxAirJumps) {
			jumpPhase += 1;
			float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
			float alignedSpeed = Vector3.Dot(velocity, contactNormal);
			
			if (alignedSpeed > 0f) { // Are we moving away from the contact surface?
				jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
			}
			
			velocity += contactNormal * jumpSpeed;
		}
	}

	void AdjustVelocity () {
		Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
		Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

		float currentX = Vector3.Dot(velocity, xAxis); // Length of x axis is always one, so no need to divide! Just projects current velocity onto new axis
		float currentZ = Vector3.Dot(velocity, zAxis);

		float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
		float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

		velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
	}

	void OnCollisionEnter (Collision collision) {
		EvaluateCollision(collision);
	}

	void OnCollisionStay (Collision collision) {
		EvaluateCollision(collision);
	}

	void EvaluateCollision (Collision collision) {
		for (int i = 0; i < collision.contactCount; i++) {
			Vector3 normal = collision.GetContact(i).normal;
			if (normal.y >= minGroundDotProduct) {
				groundContactCount += 1;
				contactNormal += normal;
			}
		}
	}

	Vector3 ProjectOnContactPlane (Vector3 vector) => (vector - contactNormal * Vector3.Dot(vector, contactNormal)) / (contactNormal.magnitude * contactNormal.magnitude);
}