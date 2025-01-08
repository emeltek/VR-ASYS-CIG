using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class SnowSystem : MonoBehaviour
{
    private ParticleSystem ps;
    public float shapeIncValue = 2f;
    public float emissionIncValue = 35f;
    public float sizeIncValue = 0.01f;
 
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void Update()
    {
        var shape = ps.shape;
        var emission = ps.emission;
        var main = ps.main;
        shapeIncValue += 0.1f * Time.deltaTime;
        emissionIncValue += 0.8f * Time.deltaTime;
        if (sizeIncValue < 5)
        {
            sizeIncValue += 0.1f * Time.deltaTime;
            main.startSize = sizeIncValue;
        }
        emission.rateOverTime = emissionIncValue;
        shape.radius = shapeIncValue;
        transform.position += Vector3.down * 0.01f ;
        transform.position += Vector3.left * 0.01f ;
    }
}