using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
class CustomGravityRigidBody : MonoBehaviour {

	[SerializeField]
	bool floatToSleep = true;

	Rigidbody body;

	void Awake () {
		body = GetComponent<Rigidbody>();
		body.useGravity = false;
	}

	float floatDelay;


	void FixedUpdate () {

		bool asleep = body.IsSleeping();
		bool floating = floatDelay > 0f;
		bool active = body.velocity.sqrMagnitude > 0.001f;
		
		Color renderColor;

		if (asleep) {
			renderColor = Color.gray;
		}
		else if (active) {
			renderColor = Color.red;
		}
		else if (floating) {
			renderColor = Color.yellow;
		}
		else {
			renderColor = Color.blue;
		}

		GetComponent<Renderer>().material.SetColor("_Color", renderColor);
		
		if (floatToSleep) {
			if (body.IsSleeping()) {
				floatDelay = 0f;
				return;
			}

			if (!active) {
				floatDelay += Time.deltaTime;
				if (floatDelay > 1f) {
					return;
				}
			}
		}

		body.AddForce(CustomGravity.GetGravity(body.position), ForceMode.Acceleration);
	}

}