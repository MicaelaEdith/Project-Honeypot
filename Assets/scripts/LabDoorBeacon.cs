using UnityEngine;

[DisallowMultipleComponent]
public class LabDoorBeacon : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Light beaconLight;
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private MeshRenderer columnRenderer;

    [SerializeField] private Color glowColor = new Color(0.94509804f, 0.94901961f, 0.62745098f, 1f);
    [SerializeField] private float columnEmission = 1.6f;
    [SerializeField] private float columnScrollSpeed = 0.35f;

    [SerializeField] private float idleEmission = 0.15f;
    [SerializeField] private float activeEmission = 3.5f;
    [SerializeField] private float idleIntensity = 0f;
    [SerializeField] private float activeIntensity = 8f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseAmplitude = 0.25f;
    [SerializeField] private float activationSpeed = 2f;
    [SerializeField] private float flashBoost = 3f;
    [SerializeField] private float flashDecay = 4f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock propertyBlock;
    private Material columnMaterial;
    private float activation;
    private float flash;
    private bool victory;

    private void Awake()
    {
        ResolveReferences();
        propertyBlock = new MaterialPropertyBlock();

        if (columnRenderer != null)
        {
            columnMaterial = columnRenderer.material;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (gameManager != null)
        {
            gameManager.OnVictory -= HandleVictory;
            gameManager.OnVictory += HandleVictory;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnVictory -= HandleVictory;
        }
    }

    private void Update()
    {
        float target = (victory || IsGoalActive()) ? 1f : 0f;
        activation = Mathf.MoveTowards(activation, target, activationSpeed * Time.deltaTime);
        flash = Mathf.MoveTowards(flash, 0f, flashDecay * Time.deltaTime);

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        float boost = 1f + flash * flashBoost;

        ApplyLight(pulse, boost);
        ApplyEmission(pulse, boost);
        ApplyColumn(pulse, boost);
    }

    private bool IsGoalActive()
    {
        return gameManager != null
            && gameManager.TotalEnemies > 0
            && gameManager.RemainingEnemies <= 0
            && !gameManager.IsFinished;
    }

    private void HandleVictory()
    {
        victory = true;
        activation = 1f;
        flash = 1f;
    }

    private void ApplyLight(float pulse, float boost)
    {
        if (beaconLight == null)
        {
            return;
        }

        beaconLight.intensity = Mathf.Lerp(idleIntensity, activeIntensity, activation) * pulse * boost;
    }

    private void ApplyEmission(float pulse, float boost)
    {
        if (glowRenderer == null)
        {
            return;
        }

        float intensity = Mathf.Lerp(idleEmission, activeEmission, activation) * pulse * boost;
        Color color = glowColor * intensity;
        color.a = Mathf.Clamp01(intensity);

        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(EmissionColorId, color);
        glowRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyColumn(float pulse, float boost)
    {
        if (columnMaterial == null)
        {
            return;
        }

        float intensity = Mathf.Lerp(idleEmission, activeEmission, activation) * pulse * boost * columnEmission;
        Color color = glowColor * intensity;
        color.a = Mathf.Clamp01(intensity);

        columnMaterial.SetColor(BaseColorId, color);
        columnMaterial.SetColor(EmissionColorId, color);

        float offset = Mathf.Repeat(-Time.time * columnScrollSpeed, 1f);
        columnMaterial.SetTextureOffset(BaseMapId, new Vector2(0f, offset));
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
    }
}
