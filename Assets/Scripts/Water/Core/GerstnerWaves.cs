using UnityEngine;

/// <summary>
/// Classe estática com cálculos matemáticos para ondas Gerstner.
/// Usada tanto pelo shader (parâmetros sincronizados) quanto pela lógica de gameplay.
/// </summary>
public static class GerstnerWaves
{
    /// <summary>
    /// Calcula a altura da superfície da água e a normal em uma posição mundial.
    /// </summary>
    /// <param name="worldPosition">Posição XZ no mundo</param>
    /// <param name="time">Tempo atual (Time.time)</param>
    /// <param name="settings">Configurações das ondas</param>
    /// <returns>Tupla com altura Y e vetor normal da superfície</returns>
    public static (float height, Vector3 normal) CalculateWaveData(
        Vector3 worldPosition, 
        float time, 
        WaveSettings settings)
    {
        if (settings == null || settings.waveLayers == null || settings.waveLayers.Length == 0)
        {
            return (settings?.baseWaterLevel ?? 0f, Vector3.up);
        }

        float totalHeight = settings.baseWaterLevel;
        Vector3 totalDisplacement = Vector3.zero;
        Vector3 tangent = Vector3.zero;
        Vector3 binormal = Vector3.zero;

        foreach (var layer in settings.waveLayers)
        {
            var (displacement, t, b) = CalculateGerstnerWave(
                worldPosition.x,
                worldPosition.z,
                time * settings.globalSpeedMultiplier,
                layer,
                settings.globalAmplitudeMultiplier
            );

            totalDisplacement += displacement;
            tangent += t;
            binormal += b;
        }

        totalHeight += totalDisplacement.y;

        // Calcula a normal a partir das derivadas acumuladas
        Vector3 normal = Vector3.Cross(
            new Vector3(1f, tangent.y, tangent.z).normalized,
            new Vector3(binormal.x, binormal.y, 1f).normalized
        ).normalized;

        // Garante que a normal aponta para cima
        if (normal.y < 0) normal = -normal;

        return (totalHeight, normal);
    }

    /// <summary>
    /// Versão simplificada que retorna apenas a altura (mais performática)
    /// </summary>
    public static float GetHeight(Vector3 worldPosition, float time, WaveSettings settings)
    {
        if (settings == null || settings.waveLayers == null || settings.waveLayers.Length == 0)
        {
            return settings?.baseWaterLevel ?? 0f;
        }

        float totalHeight = settings.baseWaterLevel;

        foreach (var layer in settings.waveLayers)
        {
            totalHeight += CalculateWaveHeight(
                worldPosition.x,
                worldPosition.z,
                time * settings.globalSpeedMultiplier,
                layer,
                settings.globalAmplitudeMultiplier
            );
        }

        return totalHeight;
    }

    /// <summary>
    /// Calcula uma onda Gerstner individual com deslocamento XYZ e derivadas
    /// </summary>
    private static (Vector3 displacement, Vector3 tangent, Vector3 binormal) CalculateGerstnerWave(
        float x, float z, float time, WaveLayer layer, float amplitudeMultiplier)
    {
        float amplitude = layer.amplitude * amplitudeMultiplier;
        float frequency = layer.Frequency;
        Vector2 direction = layer.DirectionVector;
        float steepness = layer.steepness;
        float speed = layer.speed;

        // Parâmetro q controla a inclinação (Q = steepness / (frequency * amplitude * numWaves))
        float q = steepness;

        // Fase da onda
        float phase = speed * frequency;
        float dotProduct = direction.x * x + direction.y * z;
        float theta = frequency * dotProduct + phase * time;

        float sinTheta = Mathf.Sin(theta);
        float cosTheta = Mathf.Cos(theta);

        // Deslocamento Gerstner
        Vector3 displacement = new Vector3(
            q * amplitude * direction.x * cosTheta,
            amplitude * sinTheta,
            q * amplitude * direction.y * cosTheta
        );

        // Derivadas para cálculo da normal
        float wa = frequency * amplitude;
        
        Vector3 tangent = new Vector3(
            -q * direction.x * direction.x * wa * sinTheta,
            direction.x * wa * cosTheta,
            -q * direction.x * direction.y * wa * sinTheta
        );

        Vector3 binormal = new Vector3(
            -q * direction.x * direction.y * wa * sinTheta,
            direction.y * wa * cosTheta,
            -q * direction.y * direction.y * wa * sinTheta
        );

        return (displacement, tangent, binormal);
    }

    /// <summary>
    /// Calcula apenas a altura Y de uma onda (versão otimizada)
    /// </summary>
    private static float CalculateWaveHeight(float x, float z, float time, WaveLayer layer, float amplitudeMultiplier)
    {
        float amplitude = layer.amplitude * amplitudeMultiplier;
        float frequency = layer.Frequency;
        Vector2 direction = layer.DirectionVector;
        float phase = layer.speed * frequency;

        float theta = frequency * (direction.x * x + direction.y * z) + phase * time;
        
        return amplitude * Mathf.Sin(theta);
    }

    /// <summary>
    /// Calcula a velocidade horizontal da superfície em um ponto (útil para física)
    /// </summary>
    public static Vector3 GetSurfaceVelocity(Vector3 worldPosition, float time, WaveSettings settings)
    {
        if (settings == null || settings.waveLayers == null) return Vector3.zero;

        Vector3 velocity = Vector3.zero;
        float dt = 0.016f; // ~60fps timestep

        foreach (var layer in settings.waveLayers)
        {
            float frequency = layer.Frequency;
            Vector2 direction = layer.DirectionVector;
            float amplitude = layer.amplitude * settings.globalAmplitudeMultiplier;
            float phase = layer.speed * settings.globalSpeedMultiplier * frequency;
            float steepness = layer.steepness;

            float theta = frequency * (direction.x * worldPosition.x + direction.y * worldPosition.z) + phase * time;
            float dTheta = phase * dt;

            // Derivada temporal do deslocamento
            velocity += new Vector3(
                -steepness * amplitude * direction.x * Mathf.Sin(theta) * dTheta,
                amplitude * Mathf.Cos(theta) * dTheta,
                -steepness * amplitude * direction.y * Mathf.Sin(theta) * dTheta
            ) / dt;
        }

        return velocity;
    }
}
