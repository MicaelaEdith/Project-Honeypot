using UnityEngine;

public class EnemyTankShip : EnemyTemplate
{

    [SerializeField] private Transform muzzle;
    [SerializeField] private float muzzleLength = 0f;
    [SerializeField] private float aimConvergence = 0.2f;
    [SerializeField] private float aimJitter = 2.5f;

    protected override void AttackTick()
    {
        if (!CanAttack() || player == null)
        {
            return;
        }

        Vector3 bodyCenter = PlayerAimPoint();
        Vector3 hullCenter = HullCenter();

        Vector3 heading = HeadingToward(bodyCenter, hullCenter);
        Vector3 bore = BoreAlong(heading, hullCenter);

        float speed = ProjectileSpeedTo(bore, bodyCenter);
        float reach = Vector3.Distance(bore, bodyCenter);
        float flight = speed > 0.0001f ? reach / speed : FlightTime;

        Vector3 aimPoint = LeadAimPoint(flight);
        heading = HeadingToward(aimPoint, hullCenter);

        Vector3 direction = DirectionAlong(heading, bore, aimPoint);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction = Quaternion.Euler(
            Random.Range(-aimJitter, aimJitter),
            Random.Range(-aimJitter, aimJitter),
            0f) * direction.normalized;

        SpawnPlayerStyleProjectile(bore, direction, aimPoint, damageAmount);

        LogShot(bore, heading, direction, aimPoint, hullCenter, speed, reach, flight);
    }

    private Vector3 BoreAlong(Vector3 heading, Vector3 hullCenter)
    {
        return muzzle != null
            ? muzzle.position
            : hullCenter + heading * MuzzleDistanceAlong(heading, muzzleLength);
    }

    private void LogShot(Vector3 bore, Vector3 heading, Vector3 direction, Vector3 aimPoint, Vector3 hullCenter, float speed, float reach, float flight)
    {
        Vector3 toAim = aimPoint - bore;
        float deviation = toAim.sqrMagnitude > 0.0001f ? Vector3.Angle(direction, toAim) : 0f;

        Bounds tank = PlayerBounds();
        Vector3 nose = ForwardFlat;
        Vector3 body = tank.center;

        Debug.Log(
            $"[{name}] disparo principal\n" +
            $"  tanque.pivote   = {player.position}   (origen del transform)\n" +
            $"  tanque.bounds   = min {tank.min} / max {tank.max}  ({(tank.size.x):F1} x {(tank.size.y):F1} x {(tank.size.z):F1} u)\n" +
            $"  tanque.cuerpo   = {body}   (centro de los bounds)\n" +
            $"  desvio.pivote   = {(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(body.x, body.z))):F2} u   <-- tiene que dar 0; si no, el rig esta corrido\n" +
            $"  casco.centro    = {hullCenter}   radio = {HullRadius():F1} u   rangoataque = {AttackRange():F1} u\n" +
            $"  boca.disparo    = {bore}   ({(muzzle != null ? "muzzle explicito" : "automatica")})\n" +
            $"  morro.resuelto  = {nose}\n" +
            $"  rumbo           = {heading}\n" +
            $"  dir.bala        = {direction}\n" +
            $"  punta.con.adel  = {aimPoint}   (adelantado {flight:F2}s)\n" +
            $"  alcance         = {reach:F1} u   velocidad = {speed:F1} u/s   vuelo = {flight:F2} s\n" +
            $"  DESVIO dir->aim = {deviation:F1} grados   (convergence = {aimConvergence}, jitter = {aimJitter})",
            this);
    }

    private Vector3 HeadingToward(Vector3 aimPoint, Vector3 from)
    {
        Vector3 bearing = aimPoint - from;
        bearing.y = 0f;
        if (bearing.sqrMagnitude < 0.0001f)
        {
            Vector3 nose = ForwardFlat;
            return nose.sqrMagnitude > 0.0001f ? nose : Vector3.forward;
        }
        bearing.Normalize();

        Vector3 barrel = ForwardFlat;
        if (barrel.sqrMagnitude < 0.0001f)
        {
            return bearing;
        }

        return Vector3.Slerp(barrel, bearing, aimConvergence).normalized;
    }


    private static Vector3 DirectionAlong(Vector3 heading, Vector3 from, Vector3 aimPoint)
    {
        float horizontal = new Vector3(aimPoint.x - from.x, 0f, aimPoint.z - from.z).magnitude;
        Vector3 direction = heading * horizontal + Vector3.up * (aimPoint.y - from.y);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : heading;
    }
}
