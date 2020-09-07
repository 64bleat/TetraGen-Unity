using System.Collections;
using System.Collections.Generic;
using TetraGen;
using UnityEngine;

public class LavaLampBall : MonoBehaviour
{
    public Transform floor;
    public float gravityCold;
    public float gravityHot;
    public float coolOffDistance = 5;
    public float coldAccel = -0.1f; // factor per second
    public float hotAccel = 0.2f;
    public float tempChangeRate = 0.05f;
    private float tFactor = 0;

    private Rigidbody rb;
    private Light glow;
    private TetraGenShape shape;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        glow = GetComponent<Light>();
        shape = GetComponent<TetraGenShape>();
        tFactor = Random.value;
    }

    private void FixedUpdate()
    {
        float altitude = Mathf.Clamp01( Vector3.Dot(Vector3.up, floor.InverseTransformPoint(transform.position)) / coolOffDistance);
        float squeeze = Mathf.Pow(altitude * 2 - 1, 2) * 0.75f;


        tFactor = Mathf.Clamp01(tFactor + Mathf.Sign(Mathf.Lerp(hotAccel, coldAccel, altitude)) * tempChangeRate * Time.fixedDeltaTime);

        rb.AddForce(floor.up * Mathf.Lerp(coldAccel, hotAccel, tFactor), ForceMode.Acceleration);

        transform.localScale = new Vector3(1f + squeeze, 1f - squeeze, 1f + squeeze);

        glow.intensity = Mathf.Pow(1f - altitude, 2) * 5;

        shape.blendFactor = 0.5f + tFactor * 1.5f;
    }
}
