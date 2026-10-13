using UnityEngine;

public class EnemyMelee : EnemyTemplate
{

    [SerializeField] private float rollSpeed = 180f;

    protected override void Update()
    {
        base.Update();

        if (currentState != EnemyState.Death)
        {
            Vector3 velocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
            Vector3 axis = Vector3.Cross(Vector3.up, velocity.normalized);
            if (axis.sqrMagnitude > 0.0001f)
            {
                transform.Rotate(axis.normalized, rollSpeed * Time.deltaTime, Space.World);
            }
        }
    }
}