using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ScreenSpaceEdgeFlame : MonoBehaviour
{
    private const string FlameChildName = "__AdaptiveOutlineFlame";
    private const string ShaderName = "Custom/ScreenSpaceEdgeFlame";
    private const string DefaultSyncCardName = "Card_2";

    [Header("Sync")]
    [SerializeField] private bool syncFromCard2;
    [SerializeField] private string syncCardName = DefaultSyncCardName;
    [SerializeField] private ScreenSpaceEdgeFlame syncSource;

    [Header("开关")]
    [SerializeField] private bool effectEnabled = true;

    [Header("颜色")]
    [ColorUsage(true, true)] [SerializeField] private Color coreColor = new Color(1f, 0.12f, 0.01f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color tipColor = new Color(1f, 0.82f, 0.06f, 1f);

    [Header("形状")]
    [SerializeField] private Vector2 tailDirection = Vector2.up;
    [SerializeField] private bool directionInWorldSpace;
    [Min(0.001f)] [SerializeField] private float outlineWidth = 0.075f;
    [Min(0f)] [SerializeField] private float tailLength = 0.42f;

    [Header("动态")]
    [Min(0.01f)] [SerializeField] private float noiseScale = 9f;
    [Range(0f, 1f)] [SerializeField] private float noiseAmount = 0.72f;
    [SerializeField] private float flowSpeed = 2.4f;
    [Range(0f, 1f)] [SerializeField] private float alphaCutoff = 0.035f;

    [Header("Hover Response")]
    [SerializeField] private bool hoverResponseEnabled = true;
    [Min(0.01f)] [SerializeField] private float hoverTransitionDuration = 0.18f;
    [SerializeField] private float hoverDirectionAngleOffset;
    [Min(0f)] [SerializeField] private float hoverFlowSpeedMultiplier = 1.75f;
    [Min(0f)] [SerializeField] private float hoverTailLengthMultiplier = 2.4f;
    [Min(0f)] [SerializeField] private float hoverTailLengthBonus = 0.6f;
    [Min(0f)] [SerializeField] private float hoverOutlineWidthMultiplier = 1f;

    private SpriteRenderer sourceRenderer;
    private Image sourceImage;
    private RectTransform sourceRectTransform;
    private Canvas sourceCanvas;
    private FloatingUIFan sourceFan;
    private MeshRenderer flameRenderer;
    private MeshFilter flameMeshFilter;
    private Material flameMaterial;
    private Mesh flameMesh;
    private Sprite lastSprite;
    private Vector2 lastDirection;
    private float lastOutlineWidth;
    private float lastTailLength;
    private Bounds lastSourceBounds;
    private bool validationPending;
    private bool isHovered;
    private float hoverBlend;

    private struct FlameSettings
    {
        public bool EffectEnabled;
        public Color CoreColor;
        public Color TipColor;
        public Vector2 TailDirection;
        public bool DirectionInWorldSpace;
        public float OutlineWidth;
        public float TailLength;
        public float NoiseScale;
        public float NoiseAmount;
        public float FlowSpeed;
        public float AlphaCutoff;
    }

    public bool EffectEnabled
    {
        get => effectEnabled;
        set
        {
            effectEnabled = value;
            ApplySettings(true);
        }
    }

    public Vector2 TailDirection
    {
        get => tailDirection;
        set
        {
            tailDirection = value;
            ApplySettings(true);
        }
    }

    public void SetEffectEnabled(bool enabled) => EffectEnabled = enabled;

    public void SetHoverState(bool hovered)
    {
        isHovered = hovered;
        if (!Application.isPlaying)
            hoverBlend = hovered ? 1f : 0f;
    }

    private void OnEnable()
    {
        EnsureResources();
        ApplySettings(true);
    }

    private void OnValidate()
    {
        tailLength = Mathf.Max(0f, tailLength);
        outlineWidth = Mathf.Max(0.001f, outlineWidth);
        noiseScale = Mathf.Max(0.01f, noiseScale);
        hoverTransitionDuration = Mathf.Max(0.01f, hoverTransitionDuration);
        hoverFlowSpeedMultiplier = Mathf.Max(0f, hoverFlowSpeedMultiplier);
        hoverTailLengthMultiplier = Mathf.Max(0f, hoverTailLengthMultiplier);
        hoverTailLengthBonus = Mathf.Max(0f, hoverTailLengthBonus);
        hoverOutlineWidthMultiplier = Mathf.Max(0f, hoverOutlineWidthMultiplier);
        validationPending = true;
    }

    private void LateUpdate()
    {
        EnsureResources();
        UpdateHoverBlend();
        ApplySettings(validationPending);
        validationPending = false;
    }

    private void OnDisable()
    {
        if (flameRenderer != null)
            flameRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(flameMaterial);
        DestroyRuntimeObject(flameMesh);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void EnsureResources()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        sourceImage = GetComponent<Image>();
        sourceRectTransform = transform as RectTransform;
        sourceCanvas = sourceImage != null ? GetComponentInParent<Canvas>() : null;
        sourceFan = GetComponentInParent<FloatingUIFan>();

        if (sourceRenderer == null && sourceImage == null)
            return;

        Transform child = transform.Find(FlameChildName);
        if (child == null)
        {
            GameObject flameObject = new GameObject(FlameChildName);
            flameObject.transform.SetParent(transform, false);
            child = flameObject.transform;
        }

        SpriteRenderer obsoleteRenderer = child.GetComponent<SpriteRenderer>();
        if (obsoleteRenderer != null)
            obsoleteRenderer.enabled = false;

        flameMeshFilter = child.GetComponent<MeshFilter>();
        if (flameMeshFilter == null)
            flameMeshFilter = child.gameObject.AddComponent<MeshFilter>();

        flameRenderer = child.GetComponent<MeshRenderer>();
        if (flameRenderer == null)
            flameRenderer = child.gameObject.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            flameRenderer.enabled = false;
            return;
        }

        if (flameMaterial == null || flameMaterial.shader != shader)
        {
            DestroyRuntimeObject(flameMaterial);
            flameMaterial = new Material(shader)
            {
                name = "Screen Space Edge Flame (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            flameRenderer.sharedMaterial = flameMaterial;
        }

        if (flameMesh == null)
        {
            flameMesh = new Mesh
            {
                name = "Screen Space Edge Flame Quad",
                hideFlags = HideFlags.HideAndDontSave
            };
            flameMesh.MarkDynamic();
            flameMeshFilter.sharedMesh = flameMesh;
        }
    }

    private void UpdateHoverBlend()
    {
        ScreenSpaceEdgeFlame hoverSettingsSource = GetHoverSettingsSource();
        float target = isHovered && hoverSettingsSource.hoverResponseEnabled ? 1f : 0f;

        if (!Application.isPlaying)
        {
            hoverBlend = target;
            return;
        }

        float duration = Mathf.Max(0.01f, hoverSettingsSource.hoverTransitionDuration);
        hoverBlend = Mathf.MoveTowards(hoverBlend, target, Time.unscaledDeltaTime / duration);
    }

    private void ApplySettings(bool forceMeshRebuild)
    {
        if (flameRenderer == null || flameMaterial == null)
            return;

        FlameSettings settings = GetEffectiveSettings();
        Sprite sprite = GetSourceSprite();
        bool canRender = settings.EffectEnabled && IsSourceVisible() && sprite != null && SupportsCurrentRenderMode();
        flameRenderer.enabled = canRender;
        if (!canRender)
            return;

        Vector2 baseDirection = GetEffectiveLocalTailDirection(settings);
        Vector2 hoverDirection = GetHoverLocalDirection(baseDirection);
        Vector2 finalDirection = Vector2.Lerp(baseDirection, hoverDirection, hoverBlend);
        if (finalDirection.sqrMagnitude <= 0.0001f)
            finalDirection = baseDirection;
        finalDirection.Normalize();

        ScreenSpaceEdgeFlame hoverSettingsSource = GetHoverSettingsSource();
        float hoveredOutlineWidth = settings.OutlineWidth * hoverSettingsSource.hoverOutlineWidthMultiplier;
        float hoveredTailLength = settings.TailLength * hoverSettingsSource.hoverTailLengthMultiplier + hoverSettingsSource.hoverTailLengthBonus;
        float finalOutlineWidth = Mathf.Lerp(settings.OutlineWidth, hoveredOutlineWidth, hoverBlend);
        float finalTailLength = Mathf.Lerp(settings.TailLength, Mathf.Max(settings.TailLength, hoveredTailLength), hoverBlend);
        float finalFlowSpeed = Mathf.Lerp(settings.FlowSpeed, settings.FlowSpeed * hoverSettingsSource.hoverFlowSpeedMultiplier, hoverBlend);

        Bounds sourceBounds = GetSourceLocalBounds(sprite);
        bool meshChanged = forceMeshRebuild
            || sprite != lastSprite
            || finalDirection != lastDirection
            || !Mathf.Approximately(finalOutlineWidth, lastOutlineWidth)
            || !Mathf.Approximately(finalTailLength, lastTailLength)
            || sourceBounds.center != lastSourceBounds.center
            || sourceBounds.size != lastSourceBounds.size;

        if (meshChanged)
            RebuildEffectQuad(sprite, sourceBounds, finalDirection, finalOutlineWidth, finalTailLength);

        Rect textureRect = sprite.textureRect;
        Vector2 textureSize = new Vector2(sprite.texture.width, sprite.texture.height);
        Vector4 spriteUVRect = new Vector4(
            textureRect.x / textureSize.x,
            textureRect.y / textureSize.y,
            textureRect.width / textureSize.x,
            textureRect.height / textureSize.y);

        flameMaterial.SetTexture("_MainTex", sprite.texture);
        flameMaterial.SetVector("_SpriteUVRect", spriteUVRect);
        flameMaterial.SetVector("_SpriteBounds", new Vector4(sourceBounds.center.x, sourceBounds.center.y, sourceBounds.size.x, sourceBounds.size.y));
        flameMaterial.SetVector("_TailDirection", new Vector4(finalDirection.x, finalDirection.y, 0f, 0f));
        flameMaterial.SetColor("_CoreColor", settings.CoreColor);
        flameMaterial.SetColor("_TipColor", settings.TipColor);
        flameMaterial.SetFloat("_OutlineWidth", finalOutlineWidth);
        flameMaterial.SetFloat("_TailLength", finalTailLength);
        flameMaterial.SetFloat("_NoiseScale", settings.NoiseScale);
        flameMaterial.SetFloat("_NoiseAmount", settings.NoiseAmount);
        flameMaterial.SetFloat("_FlowSpeed", finalFlowSpeed);
        flameMaterial.SetFloat("_AlphaCutoff", settings.AlphaCutoff);
        ApplySorting();
    }

    private FlameSettings GetEffectiveSettings()
    {
        ScreenSpaceEdgeFlame source = ResolveSyncSource();
        ScreenSpaceEdgeFlame settingsSource = source != null ? source : this;

        return new FlameSettings
        {
            EffectEnabled = settingsSource.effectEnabled,
            CoreColor = settingsSource.coreColor,
            TipColor = settingsSource.tipColor,
            TailDirection = settingsSource.tailDirection,
            DirectionInWorldSpace = settingsSource.directionInWorldSpace,
            OutlineWidth = settingsSource.outlineWidth,
            TailLength = settingsSource.tailLength,
            NoiseScale = settingsSource.noiseScale,
            NoiseAmount = settingsSource.noiseAmount,
            FlowSpeed = settingsSource.flowSpeed,
            AlphaCutoff = settingsSource.alphaCutoff
        };
    }

    private ScreenSpaceEdgeFlame ResolveSyncSource()
    {
        if (syncSource != null && syncSource != this)
            return syncSource;

        if (!syncFromCard2 || transform.parent == null || gameObject.name == syncCardName)
            return null;

        Transform sibling = transform.parent.Find(syncCardName);
        if (sibling == null || sibling == transform)
            return null;

        ScreenSpaceEdgeFlame siblingFlame = sibling.GetComponent<ScreenSpaceEdgeFlame>();
        return siblingFlame != this ? siblingFlame : null;
    }

    private ScreenSpaceEdgeFlame GetHoverSettingsSource()
    {
        ScreenSpaceEdgeFlame source = ResolveSyncSource();
        return source != null ? source : this;
    }

    private Vector2 GetEffectiveLocalTailDirection(FlameSettings settings)
    {
        Vector2 direction = settings.TailDirection.sqrMagnitude > 0.0001f ? settings.TailDirection.normalized : Vector2.up;
        if (settings.DirectionInWorldSpace)
            direction = transform.InverseTransformDirection(direction).normalized;
        return direction;
    }

    private Vector2 GetHoverLocalDirection(Vector2 fallbackDirection)
    {
        ScreenSpaceEdgeFlame hoverSettingsSource = GetHoverSettingsSource();
        Vector2 spreadDirection = ResolveSpreadDirection();
        if (spreadDirection.sqrMagnitude <= 0.0001f)
            return fallbackDirection;

        float rotation = Mathf.DeltaAngle(0f, transform.localEulerAngles.z) + hoverSettingsSource.hoverDirectionAngleOffset;
        Vector2 rotatedDirection = Quaternion.Euler(0f, 0f, rotation) * spreadDirection;
        return rotatedDirection.sqrMagnitude > 0.0001f ? rotatedDirection.normalized : fallbackDirection;
    }

    private Vector2 ResolveSpreadDirection()
    {
        if (sourceFan != null)
            return sourceFan.SpreadBaseDirection;

        if (sourceRectTransform != null)
            return sourceRectTransform.anchoredPosition.x >= 0f ? Vector2.right : Vector2.left;

        return transform.localPosition.x >= 0f ? Vector2.right : Vector2.left;
    }

    private Sprite GetSourceSprite()
    {
        if (sourceImage != null && sourceImage.sprite != null)
            return sourceImage.sprite;

        if (sourceRenderer != null && sourceRenderer.sprite != null)
            return sourceRenderer.sprite;

        return sourceImage != null ? sourceImage.sprite : null;
    }

    private bool IsSourceVisible()
    {
        if (sourceImage != null && sourceImage.sprite != null)
            return sourceImage.enabled;

        if (sourceRenderer != null && sourceRenderer.sprite != null)
            return sourceRenderer.enabled;

        return sourceImage != null && sourceImage.enabled;
    }

    private bool SupportsCurrentRenderMode()
    {
        return sourceCanvas == null || sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay;
    }

    private Bounds GetSourceLocalBounds(Sprite sprite)
    {
        if (sourceImage == null && sourceRenderer != null)
            return sprite.bounds;

        if (sourceRectTransform == null)
            return new Bounds(Vector3.zero, Vector3.one);

        Rect rect = sourceRectTransform.rect;
        Vector2 size = rect.size;

        if (sourceImage != null && sourceImage.preserveAspect && sprite.rect.height > 0.0001f)
        {
            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float rectAspect = Mathf.Abs(size.y) > 0.0001f ? size.x / size.y : spriteAspect;

            if (spriteAspect > rectAspect)
                size.y = size.x / spriteAspect;
            else
                size.x = size.y * spriteAspect;
        }

        return new Bounds(rect.center, new Vector3(size.x, size.y, 0f));
    }

    private void ApplySorting()
    {
        if (sourceCanvas != null)
        {
            flameRenderer.sortingLayerID = sourceCanvas.sortingLayerID;
            flameRenderer.sortingOrder = sourceCanvas.sortingOrder - 1;
            return;
        }

        if (sourceRenderer != null)
        {
            flameRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            flameRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }
    }

    private void RebuildEffectQuad(Sprite sprite, Bounds sourceBounds, Vector2 direction, float effectiveOutlineWidth, float effectiveTailLength)
    {
        if (flameMesh == null)
            return;

        float margin = effectiveOutlineWidth * 2.5f;
        Vector2 min = (Vector2)sourceBounds.min - Vector2.one * margin;
        Vector2 max = (Vector2)sourceBounds.max + Vector2.one * margin;
        Vector2 tailOffset = direction * (effectiveTailLength + effectiveOutlineWidth);
        min += Vector2.Min(Vector2.zero, tailOffset);
        max += Vector2.Max(Vector2.zero, tailOffset);

        flameMesh.Clear();
        flameMesh.vertices = new[]
        {
            new Vector3(min.x, min.y, 0f), new Vector3(min.x, max.y, 0f),
            new Vector3(max.x, max.y, 0f), new Vector3(max.x, min.y, 0f)
        };
        flameMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        flameMesh.RecalculateBounds();

        lastSprite = sprite;
        lastDirection = direction;
        lastOutlineWidth = effectiveOutlineWidth;
        lastTailLength = effectiveTailLength;
        lastSourceBounds = sourceBounds;
    }
}
