using UnityEngine;

/// <summary>
/// Componente singleton que gerencia a superfície da água na cena.
/// Responsável por fornecer altura da água para qualquer ponto do mundo
/// e sincronizar parâmetros com o shader.
/// Suporta tanto ondas Gerstner (CPU) quanto FFT (GPU).
/// </summary>
public class WaterSurface : MonoBehaviour
{
    #region Singleton
    private static WaterSurface _instance;
    public static WaterSurface Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<WaterSurface>();
                if (_instance == null)
                {
                    Debug.LogWarning("WaterSurface não encontrado na cena. Criando um padrão.");
                    GameObject go = new GameObject("WaterSurface");
                    _instance = go.AddComponent<WaterSurface>();
                }
            }
            return _instance;
        }
    }
    #endregion

    public enum WaveMode
    {
        Gerstner,  // CPU-based Gerstner waves (original)
        FFT        // GPU-based FFT waves (Sea of Thieves style)
    }

    [Header("Mode")]
    [Tooltip("Escolha entre ondas Gerstner (CPU) ou FFT (GPU)")]
    public WaveMode waveMode = WaveMode.FFT;

    [Header("Wave Configuration (Gerstner Only)")]
    [Tooltip("Configurações das ondas (ScriptableObject)")]
    public WaveSettings waveSettings;

    [Header("Visual")]
    [Tooltip("Material da água para sincronização de parâmetros")]
    public Material waterMaterial;

    [Header("Performance")]
    [Tooltip("Intervalo de atualização do shader (0 = todo frame)")]
    [Range(0f, 0.1f)]
    public float shaderUpdateInterval = 0.016f;

    [Header("Debug")]
    public bool showDebugGizmos = false;
    public Vector3 debugSamplePoint = Vector3.zero;

    // Cache
    private float lastShaderUpdateTime;
    private FFTWaterController fftController;
    
    private static readonly int WaveAmplitudeID = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int WaveFrequencyID = Shader.PropertyToID("_WaveFrequency");
    private static readonly int WaveSpeedID = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveDirectionID = Shader.PropertyToID("_WaveDirection");
    private static readonly int WaveSteepnessID = Shader.PropertyToID("_WaveSteepness");

    private static readonly int Wave2AmplitudeID = Shader.PropertyToID("_Wave2Amplitude");
    private static readonly int Wave2FrequencyID = Shader.PropertyToID("_Wave2Frequency");
    private static readonly int Wave2SpeedID = Shader.PropertyToID("_Wave2Speed");
    private static readonly int Wave2DirectionID = Shader.PropertyToID("_Wave2Direction");
    private static readonly int Wave2SteepnessID = Shader.PropertyToID("_Wave2Steepness");

    private static readonly int Wave3AmplitudeID = Shader.PropertyToID("_Wave3Amplitude");
    private static readonly int Wave3FrequencyID = Shader.PropertyToID("_Wave3Frequency");
    private static readonly int Wave3SpeedID = Shader.PropertyToID("_Wave3Speed");
    private static readonly int Wave3DirectionID = Shader.PropertyToID("_Wave3Direction");
    private static readonly int Wave3SteepnessID = Shader.PropertyToID("_Wave3Steepness");

    private static readonly int Wave4AmplitudeID = Shader.PropertyToID("_Wave4Amplitude");
    private static readonly int Wave4FrequencyID = Shader.PropertyToID("_Wave4Frequency");
    private static readonly int Wave4SpeedID = Shader.PropertyToID("_Wave4Speed");
    private static readonly int Wave4DirectionID = Shader.PropertyToID("_Wave4Direction");
    private static readonly int Wave4SteepnessID = Shader.PropertyToID("_Wave4Steepness");

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Múltiplas instâncias de WaterSurface detectadas. Destruindo duplicata.");
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Cria configurações padrão se não houver
        if (waveSettings == null)
        {
            waveSettings = ScriptableObject.CreateInstance<WaveSettings>();
            Debug.LogWarning("WaveSettings não atribuído. Usando configurações padrão.");
        }
    }

    private void Start()
    {
        // Try to find FFT controller
        fftController = FFTWaterController.Instance;
        
        if (waveMode == WaveMode.FFT && fftController == null)
        {
            Debug.LogWarning("WaterSurface: FFT mode selected but FFTWaterController not found. Falling back to Gerstner.");
            waveMode = WaveMode.Gerstner;
        }
        else if (waveMode == WaveMode.FFT && fftController != null)
        {
            UpdateFFTMaterial();
        }
    }

    /// <summary>
    /// Forces the water material to use textures from the FFT controller.
    /// </summary>
    public void UpdateFFTMaterial()
    {
        if (fftController == null) fftController = FFTWaterController.Instance;
        
        if (fftController != null && waterMaterial != null && waveMode == WaveMode.FFT)
        {
            waterMaterial.SetTexture("_DisplacementMap", fftController.DisplacementMap);
            waterMaterial.SetTexture("_NormalFoamMap", fftController.NormalFoamMap);
            waterMaterial.SetFloat("_OceanSize", fftController.OceanSize);
            Debug.Log($"WaterSurface: FFT Textures assigned to {waterMaterial.name}");
        }
    }

    private void Update()
    {
        // Sincroniza parâmetros com o shader periodicamente (only for Gerstner mode)
        if (waveMode == WaveMode.Gerstner && waterMaterial != null && Time.time - lastShaderUpdateTime >= shaderUpdateInterval)
        {
            SyncShaderParameters();
            lastShaderUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Retorna a altura da água em uma posição mundial XZ.
    /// Este é o método principal para gameplay.
    /// </summary>
    public float GetWaterHeight(Vector3 worldPosition)
    {
        if (waveMode == WaveMode.FFT && fftController != null)
        {
            return fftController.GetWaterHeight(worldPosition, waveSettings?.baseWaterLevel ?? 0f);
        }
        
        return GerstnerWaves.GetHeight(worldPosition, Time.time, waveSettings);
    }

    /// <summary>
    /// Retorna a altura da água e a normal da superfície.
    /// Útil para orientar objetos flutuantes.
    /// </summary>
    public (float height, Vector3 normal) GetWaterData(Vector3 worldPosition)
    {
        if (waveMode == WaveMode.FFT && fftController != null)
        {
            // For FFT, we approximate the normal using height differences
            float h = fftController.GetWaterHeight(worldPosition, waveSettings?.baseWaterLevel ?? 0f);
            float hx = fftController.GetWaterHeight(worldPosition + Vector3.right * 0.5f, waveSettings?.baseWaterLevel ?? 0f);
            float hz = fftController.GetWaterHeight(worldPosition + Vector3.forward * 0.5f, waveSettings?.baseWaterLevel ?? 0f);
            
            Vector3 normal = Vector3.Cross(
                new Vector3(0f, hz - h, 0.5f),
                new Vector3(0.5f, hx - h, 0f)
            ).normalized;
            
            return (h, normal);
        }
        
        return GerstnerWaves.CalculateWaveData(worldPosition, Time.time, waveSettings);
    }

    /// <summary>
    /// Retorna a profundidade da água em relação a um ponto.
    /// Valores positivos = submerso, negativos = acima da água.
    /// </summary>
    public float GetWaterDepth(Vector3 worldPosition)
    {
        float waterHeight = GetWaterHeight(worldPosition);
        return waterHeight - worldPosition.y;
    }

    /// <summary>
    /// Verifica se um ponto está submerso.
    /// </summary>
    public bool IsUnderwater(Vector3 worldPosition)
    {
        return GetWaterDepth(worldPosition) > 0f;
    }

    /// <summary>
    /// Retorna a velocidade da superfície (para física de objetos)
    /// </summary>
    public Vector3 GetSurfaceVelocity(Vector3 worldPosition)
    {
        return GerstnerWaves.GetSurfaceVelocity(worldPosition, Time.time, waveSettings);
    }

    /// <summary>
    /// Nível base da água (sem ondas)
    /// </summary>
    public float BaseWaterLevel => waveSettings?.baseWaterLevel ?? 0f;

    /// <summary>
    /// Altura máxima que as ondas podem atingir
    /// </summary>
    public float MaxWaveHeight => waveSettings?.MaxWaveHeight ?? 0f;

    /// <summary>
    /// Sincroniza parâmetros do C# com o shader.
    /// Chamado automaticamente, mas pode ser forçado manualmente.
    /// </summary>
    public void SyncShaderParameters()
    {
        if (waterMaterial == null || waveSettings == null) return;

        // Multiplicadores globais
        float ampMult = waveSettings.globalAmplitudeMultiplier;
        float speedMult = waveSettings.globalSpeedMultiplier;

        // Sincroniza primeira camada
        if (waveSettings.waveLayers != null && waveSettings.waveLayers.Length > 0)
        {
            var w = waveSettings.waveLayers[0];
            waterMaterial.SetFloat(WaveAmplitudeID, w.amplitude * ampMult);
            waterMaterial.SetFloat(WaveFrequencyID, w.Frequency);
            waterMaterial.SetFloat(WaveSpeedID, w.speed * speedMult);
            waterMaterial.SetVector(WaveDirectionID, new Vector4(w.DirectionVector.x, w.DirectionVector.y, 0, 0));
            waterMaterial.SetFloat(WaveSteepnessID, w.steepness);
        }

        // Sincroniza segunda camada
        if (waveSettings.waveLayers != null && waveSettings.waveLayers.Length > 1)
        {
            var w = waveSettings.waveLayers[1];
            waterMaterial.SetFloat(Wave2AmplitudeID, w.amplitude * ampMult);
            waterMaterial.SetFloat(Wave2FrequencyID, w.Frequency);
            waterMaterial.SetFloat(Wave2SpeedID, w.speed * speedMult);
            waterMaterial.SetVector(Wave2DirectionID, new Vector4(w.DirectionVector.x, w.DirectionVector.y, 0, 0));
            waterMaterial.SetFloat(Wave2SteepnessID, w.steepness);
        }

        // Sincroniza terceira camada
        if (waveSettings.waveLayers != null && waveSettings.waveLayers.Length > 2)
        {
            var w = waveSettings.waveLayers[2];
            waterMaterial.SetFloat(Wave3AmplitudeID, w.amplitude * ampMult);
            waterMaterial.SetFloat(Wave3FrequencyID, w.Frequency);
            waterMaterial.SetFloat(Wave3SpeedID, w.speed * speedMult);
            waterMaterial.SetVector(Wave3DirectionID, new Vector4(w.DirectionVector.x, w.DirectionVector.y, 0, 0));
            waterMaterial.SetFloat(Wave3SteepnessID, w.steepness);
        }

        // Sincroniza quarta camada
        if (waveSettings.waveLayers != null && waveSettings.waveLayers.Length > 3)
        {
            var w = waveSettings.waveLayers[3];
            waterMaterial.SetFloat(Wave4AmplitudeID, w.amplitude * ampMult);
            waterMaterial.SetFloat(Wave4FrequencyID, w.Frequency);
            waterMaterial.SetFloat(Wave4SpeedID, w.speed * speedMult);
            waterMaterial.SetVector(Wave4DirectionID, new Vector4(w.DirectionVector.x, w.DirectionVector.y, 0, 0));
            waterMaterial.SetFloat(Wave4SteepnessID, w.steepness);
        }
    }

    /// <summary>
    /// Força uma atualização global das ondas (útil para mudanças de clima)
    /// </summary>
    public void SetWaveSettings(WaveSettings newSettings)
    {
        waveSettings = newSettings;
        SyncShaderParameters();
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || waveSettings == null) return;

        // Desenha o nível base da água
        Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
        Vector3 center = transform.position;
        center.y = waveSettings.baseWaterLevel;
        Gizmos.DrawCube(center, new Vector3(50f, 0.1f, 50f));

        // Desenha ponto de amostragem de debug
        if (Application.isPlaying)
        {
            float height = GetWaterHeight(debugSamplePoint);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(debugSamplePoint.x, height, debugSamplePoint.z), 0.5f);
            Gizmos.DrawLine(debugSamplePoint, new Vector3(debugSamplePoint.x, height, debugSamplePoint.z));
        }
    }
}
