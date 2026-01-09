using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hides the third-person character mesh when in first-person while keeping shadows (optional).
/// Attach to the player root and reference it from CameraModeManager.
/// </summary>
public class HideFirstPersonModel : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Root transform whose renderers will be hidden in first-person. Defaults to this object.")]
    public Transform renderRoot;

    [Tooltip("Additional renderers to include (e.g., weapons, gear).")]
    public List<Renderer> extraRenderers = new List<Renderer>();

    [Header("Behavior")]
    [Tooltip("Keep shadows by switching renderers to ShadowsOnly instead of disabling them.")]
    public bool shadowsOnly = true;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>();
    private readonly Dictionary<Renderer, ShadowCastingMode> originalShadows = new Dictionary<Renderer, ShadowCastingMode>();

    private void Awake()
    {
        if (renderRoot == null)
            renderRoot = transform;

        CacheRenderers();
    }

    private void CacheRenderers()
    {
        cachedRenderers.Clear();
        originalShadows.Clear();

        if (renderRoot != null)
            cachedRenderers.AddRange(renderRoot.GetComponentsInChildren<Renderer>(includeInactive: true));

        if (extraRenderers != null)
        {
            foreach (var r in extraRenderers)
            {
                if (r != null && !cachedRenderers.Contains(r))
                    cachedRenderers.Add(r);
            }
        }

        foreach (var r in cachedRenderers)
        {
            originalShadows[r] = r.shadowCastingMode;
        }
    }

    public void SetFirstPerson(bool isFirstPerson)
    {
        if (cachedRenderers.Count == 0)
            CacheRenderers();

        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;

            if (isFirstPerson)
            {
                if (shadowsOnly)
                {
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    r.enabled = true; // keep active so shadows render
                }
                else
                {
                    r.enabled = false;
                }
            }
            else
            {
                // Restore
                r.enabled = true;
                if (originalShadows.TryGetValue(r, out var mode))
                    r.shadowCastingMode = mode;
            }
        }
    }
}
