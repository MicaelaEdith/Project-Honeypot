using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class HoneyPotActivator : MonoBehaviour
{

    [SerializeField] private GameObject honeypotPrefab;
    [SerializeField] private int maxHoneypots = 10;
    [SerializeField] private float degreesToActivate = 360f;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [SerializeField] private float cooldown = 0.3f;

    public event System.Action<int> OnHoneypotCountChanged;

    public int RemainingHoneypots => remainingHoneypots;

    private int remainingHoneypots;
    private float accumulatedDegrees;
    private float previousYaw;
    private float nextActivationTime;

    private void Start()
    {
        remainingHoneypots = maxHoneypots;
        previousYaw = transform.eulerAngles.y;
        OnHoneypotCountChanged?.Invoke(remainingHoneypots);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool throttling = keyboard.wKey.isPressed || keyboard.sKey.isPressed;
        bool turning = keyboard.aKey.isPressed || keyboard.dKey.isPressed;

        if (throttling)
        {
            accumulatedDegrees = 0f;
        }
        else if (turning)
        {
            float yaw = transform.eulerAngles.y;
            float delta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
            accumulatedDegrees += delta;

            if (accumulatedDegrees >= degreesToActivate)
            {
                accumulatedDegrees = 0f;
                TryActivate();
            }
        }
        else if (accumulatedDegrees > 0f)
        {
            // acumulador parcial: no resetea hasta tocar W/S
        }

        previousYaw = transform.eulerAngles.y;
    }

    private void TryActivate()
    {
        if (Time.time < nextActivationTime)
        {
            return;
        }

        if (honeypotPrefab == null)
        {
            Debug.LogError("[HoneyPotActivator] honeypotPrefab sin asignar.", this);
            return;
        }

        if (remainingHoneypots <= 0)
        {
            return;
        }

        Instantiate(honeypotPrefab, transform.position + spawnOffset, Quaternion.identity);
        remainingHoneypots--;
        nextActivationTime = Time.time + cooldown;
        AudioManager.Instance?.PlayHoneypot();
        OnHoneypotCountChanged?.Invoke(remainingHoneypots);
    }
}
