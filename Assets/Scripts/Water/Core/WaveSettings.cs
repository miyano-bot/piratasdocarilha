using UnityEngine;

/// <summary>
/// ScriptableObject que define parâmetros configuráveis para ondas Gerstner.
/// Permite criar diferentes presets (mar calmo, tempestade, costa, etc.)
/// </summary>
[CreateAssetMenu(fileName = "WaveSettings", menuName = "Water/Wave Settings")]
public class WaveSettings : ScriptableObject
{
    [Header("Wave Layers")]
    [Tooltip("Camadas de onda independentes que se somam")]
    public WaveLayer[] waveLayers = new WaveLayer[]
    {
        new WaveLayer { amplitude = 0.6f, wavelength = 25f, speed = 0.8f, direction = 0f, steepness = 0.4f },
        new WaveLayer { amplitude = 0.3f, wavelength = 12f, speed = 1.2f, direction = 45f, steepness = 0.2f },
        new WaveLayer { amplitude = 0.2f, wavelength = 8f, speed = 1.5f, direction = 160f, steepness = 0.15f },
        new WaveLayer { amplitude = 0.15f, wavelength = 5f, speed = 2.0f, direction = 290f, steepness = 0.1f }
    };

    [Header("Global Settings")]
    [Tooltip("Multiplicador global de altura das ondas")]
    [Range(0f, 2f)]
    public float globalAmplitudeMultiplier = 1f;

    [Tooltip("Multiplicador global de velocidade das ondas")]
    [Range(0f, 2f)]
    public float globalSpeedMultiplier = 1f;

    [Header("Water Level")]
    [Tooltip("Altura base da superfície da água (Y)")]
    public float baseWaterLevel = 0f;

    /// <summary>
    /// Retorna a altura máxima que as ondas podem atingir (para detecção)
    /// </summary>
    public float MaxWaveHeight
    {
        get
        {
            float max = 0f;
            foreach (var layer in waveLayers)
            {
                max += layer.amplitude;
            }
            return max * globalAmplitudeMultiplier;
        }
    }
}

/// <summary>
/// Representa uma camada individual de onda Gerstner
/// </summary>
[System.Serializable]
public class WaveLayer
{
    [Tooltip("Altura da onda em unidades")]
    [Range(0f, 5f)]
    public float amplitude = 1f;

    [Tooltip("Distância entre cristas da onda")]
    [Range(1f, 50f)]
    public float wavelength = 10f;

    [Tooltip("Velocidade de movimento da onda")]
    [Range(0f, 5f)]
    public float speed = 1f;

    [Tooltip("Direção da onda em graus (0 = +Z)")]
    [Range(0f, 360f)]
    public float direction = 0f;

    [Tooltip("Inclinação/intensidade Gerstner (0 = senoidal, 1 = máximo)")]
    [Range(0f, 1f)]
    public float steepness = 0.5f;

    /// <summary>
    /// Retorna a direção como vetor normalizado XZ
    /// </summary>
    public Vector2 DirectionVector
    {
        get
        {
            float rad = direction * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }
    }

    /// <summary>
    /// Retorna a frequência angular (omega) da onda
    /// </summary>
    public float Frequency => 2f * Mathf.PI / wavelength;
}
