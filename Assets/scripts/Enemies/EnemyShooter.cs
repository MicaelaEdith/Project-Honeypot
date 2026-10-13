using UnityEngine;

public class EnemyShooter : EnemyTemplate
{

    [SerializeField] private float muzzleRadius = 0f;
    [SerializeField] private float spreadAngle = 120f;
    [SerializeField] private float maxArcHalfAngle = 60f;
    [SerializeField] private float aimJitter = 4f;

    protected override void AttackTick()
    {
        if (!CanAttack() || player == null)
        {
            return;
        }

        Vector3 nose = ForwardFlat;
        if (nose.sqrMagnitude < 0.0001f)
        {
            nose = Vector3.forward;
        }

        Vector3 target = PlayerAimPoint();

        Vector3 bearing = target - transform.position;
        bearing.y = 0f;
        if (bearing.sqrMagnitude < 0.0001f)
        {
            return;
        }
        bearing.Normalize();

        int slot = FacingCannonSlot(nose, bearing);
        if (slot == int.MinValue)
        {
            return;
        }

        Vector3 cannonDirection = Quaternion.Euler(0f, spreadAngle * slot, 0f) * nose;
        cannonDirection.Normalize();

        Vector3 muzzle = transform.position + cannonDirection * MuzzleDistanceAlong(cannonDirection, muzzleRadius);

        Vector3 aim = target - muzzle;
        if (aim.sqrMagnitude < 0.0001f)
        {
            return;
        }
        aim.Normalize();

        if (aimJitter > 0f)
        {
            aim = Quaternion.Euler(0f, Random.Range(-aimJitter, aimJitter), 0f) * aim;
        }

        SpawnEnemyProjectile(muzzle, aim, target, damageAmount);
    }

    private int FacingCannonSlot(Vector3 nose, Vector3 toPlayer)
    {
        float bearing = Vector3.SignedAngle(nose, toPlayer, Vector3.up);
        int slot = 0;
        float bestError = float.MaxValue;

        for (int i = -1; i <= 1; i++)
        {
            float error = Mathf.Abs(Mathf.DeltaAngle(spreadAngle * i, bearing));
            if (error < bestError)
            {
                bestError = error;
                slot = i;
            }
        }

        return bestError <= maxArcHalfAngle ? slot : int.MinValue;
    }
}
