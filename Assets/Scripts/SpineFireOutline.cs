// =============================================================================
//  SpineFireOutline.cs
//  Built-in 2D pipeline version of the "screen-space outline + fire distortion"
//  effect shown in Bilibili BV1eppyzREJ6 (which was done in URP).
//
//  How it works
//  ------------
//  A hidden SECOND camera (the "mask camera") shares this camera's transform and
//  renders ONLY the objects on [outlineLayer] into a screen-sized
//  RenderTexture, using the objects' real materials so their alpha is captured
//  exactly (works for SpriteRenderer AND Spine SkeletonAnimation out of the box
//  - no replacement shader, no per-renderer texture juggling).
//
//  Every frame:
//     1.  OnPreCull -> maskCam.Render() -> maskRT holds the silhouettes.
//     2.  Main camera renders the full scene to the screen.
//     3.  OnRenderImage -> FireOutlinePost shader:
//           - edge-detects the mask (cross + diagonal sampling over N radii)
//           - wobbles the band with value-noise distortion
//           - drives a fire color ramp + flicker
//           - composites additive flame + a dark core line over the screen.
//
//  Setup
//  -----
//     1. Put BOTH files into your project:  SpineFireOutline.cs  +  FireOutlinePost.shader
//     2. Create a layer called e.g. "FireOutline" (Project Settings -> Tags and Layers).
//     3. Set the SpriteRenderer / Spine objects you want outlined to that layer.
//     4. Add this component to your MAIN camera.
//     5. Set [Outline Layer] = "FireOutline".
//     6. Assign a fire color ramp texture to [Fire Color Texture]
//        (a 256x4 horizontal gradient: red on the left -> orange -> yellow -> near-white on the right,
//         alpha 1 everywhere). If you leave it empty the effect still runs (white ramp).
//
//  Notes
//  -----
//  - Built-in renderer only. For URP use a CustomRenderFeature (not this).
//  - The mask camera renders the objects twice per frame (perf cost is one extra
//    draw call batch of the outlined objects). For 2D this is negligible.
//  - If the outline appears vertically flipped on your platform, the mask UV flip
//    is handled via UNITY_UV_STARTS_AT_TOP inside the shader.
// =============================================================================
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class SpineFireOutline : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Objects on this layer (and only this layer) receive the fire outline.")]
    public LayerMask outlineLayer = 1;

    [Header("Outline")]
    [Range(1, 8)] public float outlineWidth = 3f;
    public Color outlineCoreColor = new Color(0.55f, 0.08f, 0f, 1f);
    [Range(0, 1)] public float outlineCoreHardness = 0.8f;

    [Header("Fire")]
    [Tooltip("Horizontal ramp: red(left) -> orange -> yellow -> white(right).")]
    public Texture2D fireColorTexture;
    public Color fireTint = new Color(1f, 0.6f, 0.2f, 1f);
    [Range(0, 3)] public float fireIntensity = 1.2f;
    public float fireSpeed = 1.5f;
    public float fireNoiseScale = 6f;
    public float distortionStrength = 0.012f;

    Camera        mainCam;
    Camera        maskCam;
    RenderTexture maskRT;
    Material      postMat;

    static readonly int ID_Mask      = Shader.PropertyToID("_MaskTex");
    static readonly int ID_Fire      = Shader.PropertyToID("_FireColorTex");
    static readonly int ID_Width     = Shader.PropertyToID("_OutlineWidth");
    static readonly int ID_Hardness  = Shader.PropertyToID("_OutlineHardness");
    static readonly int ID_Distort   = Shader.PropertyToID("_DistortStrength");
    static readonly int ID_FireSpeed = Shader.PropertyToID("_FireSpeed");
    static readonly int ID_FireScale = Shader.PropertyToID("_FireScale");
    static readonly int ID_FireInt   = Shader.PropertyToID("_FireIntensity");
    static readonly int ID_FireTint  = Shader.PropertyToID("_FireTint");
    static readonly int ID_OutCol   = Shader.PropertyToID("_OutlineColor");

    void OnEnable()
    {
        mainCam = GetComponent<Camera>();
        EnsurePostMaterial();
        EnsureMaskCamera();
        EnsureRT();
    }

    void EnsurePostMaterial()
    {
        if (postMat != null) return;
        var sh = Shader.Find("Hidden/FireOutlinePost");
        if (sh == null) { Debug.LogError("[SpineFireOutline] FireOutlinePost shader not found. " +
                                         "Make sure FireOutlinePost.shader is in the project and is included in 'Always Included Shaders' or referenced by a material."); return; }
        postMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
    }

    void EnsureMaskCamera()
    {
        if (maskCam != null) return;
        var go = new GameObject("__FireOutlineMaskCam", typeof(Camera));
        go.transform.SetParent(transform, false);
        go.hideFlags = HideFlags.HideAndDontSave;
        maskCam = go.GetComponent<Camera>();
        maskCam.enabled          = false;             // we render it manually
        maskCam.clearFlags       = CameraClearFlags.SolidColor;
        maskCam.backgroundColor = new Color(0, 0, 0, 0);
        maskCam.allowMSAA        = false;
        maskCam.allowHDR        = false;
        maskCam.depthTextureMode = DepthTextureMode.None;
        // also silence any audio listener it might grab
        if (go.GetComponent<AudioListener>() != null) DestroyImmediate(go.GetComponent<AudioListener>());
        maskCam.cullingMask = outlineLayer;
    }

    void EnsureRT()
    {
        int w = mainCam.pixelWidth  > 0 ? mainCam.pixelWidth  : Screen.width;
        int h = mainCam.pixelHeight > 0 ? mainCam.pixelHeight : Screen.height;
        if (maskRT != null && maskRT.width == w && maskRT.height == h) return;
        if (maskRT != null) maskRT.Release();
        maskRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        maskRT.filterMode = FilterMode.Bilinear;
        maskRT.Create();
    }

    void Update()
    {
        EnsureRT();
        if (maskCam == null) return;
        // keep the mask camera's projection matched to the main camera
        maskCam.cullingMask       = outlineLayer;
        maskCam.orthographic      = mainCam.orthographic;
        maskCam.orthographicSize  = mainCam.orthographicSize;
        maskCam.fieldOfView       = mainCam.fieldOfView;
        maskCam.nearClipPlane     = mainCam.nearClipPlane;
        maskCam.farClipPlane      = mainCam.farClipPlane;
        maskCam.rect              = mainCam.rect;
        maskCam.targetTexture     = maskRT;
    }

    void LateUpdate()
    {
        if (maskCam != null)
            maskCam.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    // Render the mask BEFORE the main camera draws its image effects.
    void OnPreCull()
    {
        if (maskCam != null && maskRT != null && maskRT.IsCreated())
        {
            maskCam.targetTexture = maskRT;
            maskCam.Render();
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (postMat == null || maskRT == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        postMat.SetTexture(ID_Mask, maskRT);
        if (fireColorTexture != null) postMat.SetTexture(ID_Fire, fireColorTexture);
        postMat.SetFloat(ID_Width,    outlineWidth);
        postMat.SetFloat(ID_Hardness, outlineCoreHardness);
        postMat.SetFloat(ID_Distort,  distortionStrength);
        postMat.SetFloat(ID_FireSpeed, fireSpeed);
        postMat.SetFloat(ID_FireScale, fireNoiseScale);
        postMat.SetFloat(ID_FireInt,   fireIntensity);
        postMat.SetColor(ID_FireTint,  fireTint);
        postMat.SetColor(ID_OutCol,    outlineCoreColor);

        Graphics.Blit(src, dst, postMat);
    }

    void OnDisable()
    {
        if (maskCam != null) { DestroyImmediate(maskCam.gameObject); maskCam = null; }
        if (maskRT  != null) { maskRT.Release(); maskRT = null; }
        if (postMat != null) { DestroyImmediate(postMat); postMat = null; }
    }
}

