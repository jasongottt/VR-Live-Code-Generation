
using UnityEngine;
//hello
public class GeneratedBehavior : MonoBehaviour
{
void Start()
{
}

void Update()
{
    float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.25f;
    transform.localScale = new Vector3(pulse, pulse, pulse);
}

}
