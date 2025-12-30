using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

/// <summary>
/// FFT Water Controller - Orchestrates the GPU-based FFT ocean simulation.
/// Manages Compute Shaders, textures, and CPU readback for physics.
/// Inspired by Sea of Thieves ocean technology.
/// </summary>
public class FFTWaterController : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader fftOceanShader;
    
    [Header("Ocean Parameters")]
    [SerializeField] private int resolution = 256;
    [SerializeField] private float oceanSize = 100f;
    [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.5f);
    [SerializeField] private float windSpeed = 20f;
    [SerializeField] private float waveAmplitude = 1.0f;
    [SerializeField] private float choppiness = 1.5f;
    [SerializeField] private float foamBias = 0.2f;
    [SerializeField] private float timeScale = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool debugReadback = false;
    
    [Header("Output Textures (Live)")]
    [SerializeField] private RenderTexture displacementMap;
    [SerializeField] private RenderTexture normalFoamMap;

    private RenderTexture spectrumH0;
    private RenderTexture spectrumH0Conj;
    private RenderTexture spectrumHt;
    private RenderTexture spectrumHt2;
    private RenderTexture fftBufferA;
    private RenderTexture fftBufferB;
    private RenderTexture fftBufferA2;
    private RenderTexture fftBufferB2;
    
    // Kernel IDs
    private int kernelInitSpectrum;
    private int kernelUpdateSpectrum;
    private int kernelFFTStockham;
    private int kernelAssembleDisplacement;
    
    // Shader property IDs
    private static readonly int StageID = Shader.PropertyToID("_Stage");
    private static readonly int PingPongID = Shader.PropertyToID("_PingPong");
    private static readonly int FFTReadID = Shader.PropertyToID("_FFTRead");
    private static readonly int FFTWriteID = Shader.PropertyToID("_FFTWrite");
    private static readonly int ResolutionID = Shader.PropertyToID("_Resolution");
    private static readonly int OceanSizeID = Shader.PropertyToID("_OceanSize");
    private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
    private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
    private static readonly int TimeID = Shader.PropertyToID("_Time");
    private static readonly int WaveAmplitudeID = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int ChoppinessID = Shader.PropertyToID("_Choppiness");
    private static readonly int FoamBiasID = Shader.PropertyToID("_FoamBias");
    
    // CPU Readback
    private NativeArray<Color> displacementReadback;
    private bool readbackPending = false;
    private float[,] heightCache;
    
    // Singleton for easy access
    public static FFTWaterController Instance { get; private set; }
    
    // Public accessors
    public RenderTexture DisplacementMap => displacementMap;
    public RenderTexture NormalFoamMap => normalFoamMap;
    public int Resolution => resolution;
    public float OceanSize => oceanSize;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (fftOceanShader == null)
        {
            Debug.LogWarning("FFTWaterController: ComputeShader not assigned. FFT simulation disabled.");
            enabled = false;
            return;
        }

        InitializeTextures();
        InitializeKernels();
        InitializeSpectrum();
        
        heightCache = new float[resolution, resolution];

        // Ensure textures are assigned to material after they are created
        if (WaterSurface.Instance != null)
        {
            WaterSurface.Instance.UpdateFFTMaterial();
        }
    }

    private void Update()
    {
        if (fftOceanShader == null) return;
        
        UpdateSpectrum();
        PerformIFFT();
        AssembleDisplacement();
        
        // Request async readback for physics
        if (!readbackPending)
        {
            RequestHeightReadback();
        }
    }

    private void OnDestroy()
    {
        ReleaseTextures();
        
        if (displacementReadback.IsCreated)
            displacementReadback.Dispose();
    }

    #region Initialization

    private void InitializeTextures()
    {
        spectrumH0 = CreateFFTTexture("SpectrumH0");
        spectrumH0Conj = CreateFFTTexture("SpectrumH0Conj");
        spectrumHt = CreateFFTTexture("SpectrumHt");
        spectrumHt2 = CreateFFTTexture("SpectrumHt2");
        fftBufferA = CreateFFTTexture("FFTBufferA");
        fftBufferB = CreateFFTTexture("FFTBufferB");
        fftBufferA2 = CreateFFTTexture("FFTBufferA2");
        fftBufferB2 = CreateFFTTexture("FFTBufferB2");
        displacementMap = CreateFFTTexture("DisplacementMap");
        normalFoamMap = CreateFFTTexture("NormalFoamMap");
    }

    private RenderTexture CreateFFTTexture(string name)
    {
        RenderTexture rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            name = name
        };
        rt.Create();
        return rt;
    }

    private void InitializeKernels()
    {
        kernelInitSpectrum = fftOceanShader.FindKernel("InitSpectrum");
        kernelUpdateSpectrum = fftOceanShader.FindKernel("UpdateSpectrum");
        kernelFFTStockham = fftOceanShader.FindKernel("FFT_Stockham");
        kernelAssembleDisplacement = fftOceanShader.FindKernel("AssembleDisplacement");
    }

    private void InitializeSpectrum()
    {
        SetShaderParameters();
        
        fftOceanShader.SetTexture(kernelInitSpectrum, "_SpectrumH0", spectrumH0);
        fftOceanShader.SetTexture(kernelInitSpectrum, "_SpectrumH0Conj", spectrumH0Conj);
        
        int groups = Mathf.CeilToInt(resolution / 8f);
        fftOceanShader.Dispatch(kernelInitSpectrum, groups, groups, 1);
    }

    private void ReleaseTextures()
    {
        if (spectrumH0 != null) spectrumH0.Release();
        if (spectrumH0Conj != null) spectrumH0Conj.Release();
        if (spectrumHt != null) spectrumHt.Release();
        if (spectrumHt2 != null) spectrumHt2.Release();
        if (fftBufferA != null) fftBufferA.Release();
        if (fftBufferB != null) fftBufferB.Release();
        if (fftBufferA2 != null) fftBufferA2.Release();
        if (fftBufferB2 != null) fftBufferB2.Release();
        if (displacementMap != null) displacementMap.Release();
        if (normalFoamMap != null) normalFoamMap.Release();
    }

    #endregion

    #region FFT Pipeline

    private void SetShaderParameters()
    {
        fftOceanShader.SetInt(ResolutionID, resolution);
        fftOceanShader.SetFloat(OceanSizeID, oceanSize);
        fftOceanShader.SetVector(WindDirectionID, windDirection.normalized);
        fftOceanShader.SetFloat(WindSpeedID, windSpeed);
        fftOceanShader.SetFloat(WaveAmplitudeID, waveAmplitude);
        fftOceanShader.SetFloat(ChoppinessID, choppiness);
        fftOceanShader.SetFloat(FoamBiasID, foamBias);
    }

    private void UpdateSpectrum()
    {
        fftOceanShader.SetFloat(TimeID, Time.time * timeScale);
        
        fftOceanShader.SetTexture(kernelUpdateSpectrum, "_SpectrumH0", spectrumH0);
        fftOceanShader.SetTexture(kernelUpdateSpectrum, "_SpectrumH0Conj", spectrumH0Conj);
        fftOceanShader.SetTexture(kernelUpdateSpectrum, "_SpectrumHt", spectrumHt);
        fftOceanShader.SetTexture(kernelUpdateSpectrum, "_SpectrumHt2", spectrumHt2);
        
        int groups = Mathf.CeilToInt(resolution / 8f);
        fftOceanShader.Dispatch(kernelUpdateSpectrum, groups, groups, 1);
    }

    private void PerformIFFT()
    {
        // 1. Process Height (XY) and Dx (ZW)
        // Input: spectrumHt -> Result in fftBufferA or fftBufferB
        var result1 = PerformStockhamFFT(spectrumHt, fftBufferA, fftBufferB);
        
        // 2. Process Dz (XY) and JTrace (ZW)
        // Input: spectrumHt2 -> Result in fftBufferA2 or fftBufferB2
        var result2 = PerformStockhamFFT(spectrumHt2, fftBufferA2, fftBufferB2);
        
        // Bind results for assembly
        fftOceanShader.SetTexture(kernelAssembleDisplacement, "_FFTBufferB", result1);
        fftOceanShader.SetTexture(kernelAssembleDisplacement, "_FFTBufferB2", result2);
    }
    
    private RenderTexture PerformStockhamFFT(RenderTexture input, RenderTexture buf0, RenderTexture buf1)
    {
        int logN = (int)Mathf.Log(resolution, 2);
        bool swap = false;
        
        // --- Horizontal Pass (_PingPong = 0) ---
        fftOceanShader.SetInt(PingPongID, 0);
        RenderTexture source = input;
        RenderTexture dest = buf0;
        
        for (int i = 0; i < logN; i++)
        {
            fftOceanShader.SetInt(StageID, i);
            fftOceanShader.SetTexture(kernelFFTStockham, FFTReadID, source);
            fftOceanShader.SetTexture(kernelFFTStockham, FFTWriteID, dest);
            
            fftOceanShader.Dispatch(kernelFFTStockham, resolution / 8, resolution / 8, 1);
            
            // Toggle buffers
            // After first pass, source becomes one of the temp buffers
            source = dest;
            dest = (dest == buf0) ? buf1 : buf0;
            swap = !swap;
        }
        
        // --- Vertical Pass (_PingPong = 1) ---
        fftOceanShader.SetInt(PingPongID, 1);
        
        for (int i = 0; i < logN; i++)
        {
            fftOceanShader.SetInt(StageID, i);
            fftOceanShader.SetTexture(kernelFFTStockham, FFTReadID, source);
            fftOceanShader.SetTexture(kernelFFTStockham, FFTWriteID, dest);
            
            fftOceanShader.Dispatch(kernelFFTStockham, resolution / 8, resolution / 8, 1);
            
            source = dest;
            dest = (dest == buf0) ? buf1 : buf0;
            swap = !swap;
        }
        
        return source;
    }

    private void AssembleDisplacement()
    {
        // Buffers are already set in PerformIFFT via the loop result
        fftOceanShader.SetTexture(kernelAssembleDisplacement, "_DisplacementMap", displacementMap);
        fftOceanShader.SetTexture(kernelAssembleDisplacement, "_DisplacementMap", displacementMap);
        fftOceanShader.SetTexture(kernelAssembleDisplacement, "_NormalFoamMap", normalFoamMap);
        
        int groups = Mathf.CeilToInt(resolution / 8f);
        fftOceanShader.Dispatch(kernelAssembleDisplacement, groups, groups, 1);
    }

    #endregion

    #region CPU Readback (Physics)

    private void RequestHeightReadback()
    {
        if (displacementMap == null) return;
        
        readbackPending = true;
        AsyncGPUReadback.Request(displacementMap, 0, TextureFormat.RGBAFloat, OnReadbackComplete);
    }

    private void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        readbackPending = false;
        
        if (request.hasError)
        {
            if (debugReadback)
                Debug.LogWarning("FFT Readback failed!");
            return;
        }

        // Ensure cache exists (resolution may have changed)
        if (heightCache == null || heightCache.GetLength(0) != resolution || heightCache.GetLength(1) != resolution)
        {
            heightCache = new float[resolution, resolution];
        }

        displacementReadback = request.GetData<Color>();
        if (!displacementReadback.IsCreated || displacementReadback.Length == 0)
        {
            if (debugReadback)
                Debug.LogWarning("FFT Readback returned empty data.");
            return;
        }

        // Cache height values for physics queries
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                if (index < displacementReadback.Length)
                {
                    heightCache[x, y] = displacementReadback[index].g; // Y component is height
                }
            }
        }
    }

    /// <summary>
    /// Get water height at a world position for physics simulation.
    /// </summary>
    public float GetWaterHeight(Vector3 worldPosition, float baseWaterLevel = 0f)
    {
        if (heightCache == null) return baseWaterLevel;
        
        // Convert world position to UV coordinates
        float u = (worldPosition.x % oceanSize + oceanSize) / oceanSize;
        float v = (worldPosition.z % oceanSize + oceanSize) / oceanSize;
        
        // Sample from cache (bilinear interpolation)
        float fx = u * (resolution - 1);
        float fy = v * (resolution - 1);
        
        int x0 = Mathf.FloorToInt(fx) % resolution;
        int y0 = Mathf.FloorToInt(fy) % resolution;
        int x1 = (x0 + 1) % resolution;
        int y1 = (y0 + 1) % resolution;
        
        float tx = fx - Mathf.Floor(fx);
        float ty = fy - Mathf.Floor(fy);
        
        float h00 = heightCache[x0, y0];
        float h10 = heightCache[x1, y0];
        float h01 = heightCache[x0, y1];
        float h11 = heightCache[x1, y1];
        
        float h0 = Mathf.Lerp(h00, h10, tx);
        float h1 = Mathf.Lerp(h01, h11, tx);
        float height = Mathf.Lerp(h0, h1, ty);
        
        return baseWaterLevel + height;
    }

    #endregion

    #region Editor
    
    private void OnValidate()
    {
        // Reinitialize spectrum when parameters change in editor
        if (Application.isPlaying && spectrumH0 != null)
        {
            InitializeSpectrum();
        }
    }

    /// <summary>
    /// Apply a preset tuned to resemble a Sea of Thieves-style ocean.
    /// Use the component context menu in the Inspector to invoke this.
    /// </summary>
    [ContextMenu("Apply Sea of Thieves Preset")]
    private void ApplySeaOfThievesPreset()
    {
        // Recommended values — feel free to tweak in-editor
        resolution = 512;
        oceanSize = 2000f;
        windDirection = new Vector2(1f, 0.25f).normalized;
        windSpeed = 30f;
        waveAmplitude = 2.0f;
        choppiness = 3.0f;
        foamBias = 0.05f;
        timeScale = 1.0f;

        Debug.Log("FFTWaterController: Applying Sea of Thieves preset — recreating FFT textures.");

        if (fftOceanShader == null)
        {
            Debug.LogWarning("FFTWaterController: ComputeShader not assigned — cannot initialize preset.");
            return;
        }

        // Recreate GPU resources with the new resolution/parameters
        ReleaseTextures();
        InitializeTextures();
        InitializeKernels();
        InitializeSpectrum();

        if (WaterSurface.Instance != null)
            WaterSurface.Instance.UpdateFFTMaterial();
    }

    #endregion
}
