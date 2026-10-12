using UnityEngine;

/// <summary>
/// Nave circular con tres cañones en los costados (0, +spreadAngle, -spreadAngle).
/// En cada disparo sale SOLO el cañón que tiene al player dentro de su arco, y la
/// boca se calcula con el tamaño real del casco para que la bala nazca fuera de él.
/// La bala se apunta desde la boca hacia el cuerpo del tanque (no hacia el pivote),
/// para que el tiro salga derecho.
/// </summary>
public class EnemyShooter : EnemyTemplate
{
    [Header("Cañones")]
    [Tooltip("Distancia del centro a la boca del cañón. 0 = se calcula con el tamaño real del casco.")]
    [SerializeField] private float muzzleRadius = 0f;
    [Tooltip("Angulo (grados) entre los tres cañones laterales.")]
    [SerializeField] private float spreadAngle = 120f;
    [Tooltip("Medida del arco que cubre cada cañón (grados desde su eje). Con 3 cañones a 120 grados, 60 cubre los 360 sin huecos: siempre dispara uno.")]
    [SerializeField] private float maxArcHalfAngle = 60f;
    [Tooltip("Desvío aleatorio del disparo en grados, para que no salga una línea perfecta.")]
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

        // Apunta al CUERPO del tanque (centro de sus renderers), no a player.position:
        // el rig del tanque tiene el modelo corrido ~4u del pivote y apuntar al pivote
        // desviaba el tiro. Misma referencia que usa EnemyTankShip.
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

        // La bala nace en la boca, desplazada del centro de la nave: hay que apuntar
        // DESDE la boca al objetivo (en 3D), no lanzar una copia del bearing. Como la
        // boca esta fuera del eje, una direccion paralela mantiene el desvio lateral
        // durante todo el vuelo y la bala pasa de costado. Incluir 'y' tambien corrige
        // el angulo vertical para que no pase por arriba/abajo del tanque.
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

    /// <summary>
    /// Devuelve el indice del cañón (-1, 0, 1) con menor error angular respecto
    /// del bearing del player, o int.MinValue si ninguno lo tiene en su arco.
    /// </summary>
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
