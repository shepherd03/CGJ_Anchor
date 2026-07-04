using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 通过按钮点击在多个 Cinemachine Virtual Camera 之间平滑切换。
/// </summary>
[DisallowMultipleComponent]
public class CinemachineScrollZoomController : MonoBehaviour
{
    [Serializable]
    public sealed class CameraButtonBinding
    {
        [SerializeField, Tooltip("配置名称，只用于在 Inspector 里区分这一组按钮和镜头。")]
        private string bindingName;

        [SerializeField, Tooltip("点击后触发镜头切换的 UI Button。")]
        private Button button;

        [SerializeField, Tooltip("点击按钮后切换到的 Cinemachine Virtual Camera。")]
        private CinemachineVirtualCamera virtualCamera;

        [SerializeField, Tooltip("是否启用这一组按钮和镜头绑定。关闭后点击不会切换镜头。")]
        private bool enabled = true;

        public string BindingName => bindingName;
        public Button Button => button;
        public CinemachineVirtualCamera VirtualCamera => virtualCamera;
        public bool Enabled => enabled;
    }

    [Header("Cameras")]
    [SerializeField, Tooltip("主摄像机上的 CinemachineBrain。留空时自动从 Camera.main 查找。")]
    private CinemachineBrain cinemachineBrain;

    [SerializeField, Tooltip("默认虚拟镜头。初始化时会切到这个镜头；留空则使用数组里的第一个有效镜头。")]
    private CinemachineVirtualCamera defaultVirtualCamera;

    [SerializeField, Tooltip("按钮和虚拟镜头的绑定数组。你在这里配置每个按钮对应切到哪个虚拟镜头。")]
    private CameraButtonBinding[] cameraButtonBindings = Array.Empty<CameraButtonBinding>();

    [Header("Priority")]
    [SerializeField, Tooltip("被激活镜头的 Priority。必须高于未激活镜头。")]
    private int activePriority = 20;

    [SerializeField, Tooltip("未激活镜头的 Priority。数值低于激活镜头即可。")]
    private int inactivePriority = 0;

    [SerializeField, Tooltip("启动时是否初始化所有已配置虚拟镜头的 Priority。")]
    private bool initializePrioritiesOnStart = true;

    [Header("Blend")]
    [SerializeField, Tooltip("是否用这里的设置覆盖 CinemachineBrain 的默认过渡效果。")]
    private bool overrideDefaultBlend = true;

    [SerializeField, Tooltip("镜头切换的过渡曲线类型。一般用 EaseInOut。")]
    private CinemachineBlendDefinition.Style blendStyle = CinemachineBlendDefinition.Style.EaseInOut;

    [SerializeField, Min(0f), Tooltip("镜头切换过渡时间，单位秒。0 表示瞬切。")]
    private float blendTime = 0.6f;

    [Header("Buttons")]
    [SerializeField, Tooltip("是否自动给数组里的每个 Button 注册点击事件。开启后不用在 Button 的 OnClick 里手动绑定。")]
    private bool autoRegisterButtonClicks = true;

    [SerializeField, Tooltip("是否由脚本自动管理按钮是否可点击。")]
    private bool manageButtonInteractable = true;

    [SerializeField, Tooltip("当前已激活镜头对应的按钮是否禁用，防止重复点击同一个镜头。")]
    private bool disableActiveButton = true;

    [SerializeField, Tooltip("再次点击当前已激活镜头对应的按钮时，是否切回默认虚拟镜头。开启后当前按钮不会被禁用。")]
    private bool activeButtonReturnsToDefaultCamera = true;

    private readonly List<Button> registeredButtons = new List<Button>();
    private readonly List<UnityAction> registeredActions = new List<UnityAction>();
    private CinemachineVirtualCamera currentVirtualCamera;

    /// <summary>
    /// 添加组件时自动补齐主摄像机上的 CinemachineBrain。
    /// </summary>
    private void Reset()
    {
        EnsureBindingsArray();
        ResolveBrain();
    }

    /// <summary>
    /// 初始化 CinemachineBrain 和默认过渡效果。
    /// </summary>
    private void Awake()
    {
        EnsureBindingsArray();
        ResolveBrain();
        ApplyDefaultBlend();
    }

    /// <summary>
    /// 组件启用时注册按钮点击事件。
    /// </summary>
    private void OnEnable()
    {
        if (autoRegisterButtonClicks)
        {
            RegisterButtonListeners();
        }
    }

    /// <summary>
    /// 游戏开始时按配置初始化镜头优先级。
    /// </summary>
    private void Start()
    {
        if (!initializePrioritiesOnStart)
        {
            RefreshButtonInteractable();
            return;
        }

        CinemachineVirtualCamera initialCamera = GetInitialCamera();
        if (initialCamera != null)
        {
            SwitchToCamera(initialCamera);
        }
        else
        {
            RefreshButtonInteractable();
        }
    }

    /// <summary>
    /// 组件禁用时注销按钮点击事件，避免重复注册。
    /// </summary>
    private void OnDisable()
    {
        UnregisterButtonListeners();
    }

    /// <summary>
    /// 按绑定数组下标切换到对应虚拟镜头，可用于手动绑定 Button OnClick。
    /// </summary>
    public void SwitchToCameraByIndex(int index)
    {
        if (index < 0 || index >= cameraButtonBindings.Length)
        {
            Debug.LogWarning($"{nameof(CinemachineScrollZoomController)} index out of range: {index}", this);
            return;
        }

        CameraButtonBinding binding = cameraButtonBindings[index];
        if (binding == null || !binding.Enabled || binding.VirtualCamera == null)
        {
            Debug.LogWarning($"{nameof(CinemachineScrollZoomController)} has an invalid camera binding at index {index}.", this);
            return;
        }

        if (activeButtonReturnsToDefaultCamera && binding.VirtualCamera == currentVirtualCamera)
        {
            SwitchToDefaultCamera();
            return;
        }

        SwitchToCamera(binding.VirtualCamera);
    }

    /// <summary>
    /// 切回默认虚拟镜头。
    /// </summary>
    public void SwitchToDefaultCamera()
    {
        CinemachineVirtualCamera targetCamera = GetInitialCamera();
        if (targetCamera == null)
        {
            Debug.LogWarning($"{nameof(CinemachineScrollZoomController)} needs a default virtual camera.", this);
            return;
        }

        SwitchToCamera(targetCamera);
    }

    /// <summary>
    /// 切换到指定虚拟镜头。
    /// </summary>
    public void SwitchToCamera(CinemachineVirtualCamera targetCamera)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning($"{nameof(CinemachineScrollZoomController)} cannot switch to a null virtual camera.", this);
            return;
        }

        SetAllConfiguredCamerasPriority(inactivePriority);
        targetCamera.Priority = activePriority;
        currentVirtualCamera = targetCamera;

        RefreshButtonInteractable();
    }

    /// <summary>
    /// 注册所有有效按钮的点击事件。
    /// </summary>
    private void RegisterButtonListeners()
    {
        UnregisterButtonListeners();

        for (int i = 0; i < cameraButtonBindings.Length; i++)
        {
            CameraButtonBinding binding = cameraButtonBindings[i];
            if (binding == null || binding.Button == null)
            {
                continue;
            }

            int bindingIndex = i;
            UnityAction action = () => SwitchToCameraByIndex(bindingIndex);
            binding.Button.onClick.AddListener(action);

            registeredButtons.Add(binding.Button);
            registeredActions.Add(action);
        }
    }

    /// <summary>
    /// 注销已经自动注册的按钮点击事件。
    /// </summary>
    private void UnregisterButtonListeners()
    {
        for (int i = 0; i < registeredButtons.Count; i++)
        {
            if (registeredButtons[i] != null && registeredActions[i] != null)
            {
                registeredButtons[i].onClick.RemoveListener(registeredActions[i]);
            }
        }

        registeredButtons.Clear();
        registeredActions.Clear();
    }

    /// <summary>
    /// 获取初始化时应该激活的虚拟镜头。
    /// </summary>
    private CinemachineVirtualCamera GetInitialCamera()
    {
        if (defaultVirtualCamera != null)
        {
            return defaultVirtualCamera;
        }

        for (int i = 0; i < cameraButtonBindings.Length; i++)
        {
            CameraButtonBinding binding = cameraButtonBindings[i];
            if (binding != null && binding.Enabled && binding.VirtualCamera != null)
            {
                return binding.VirtualCamera;
            }
        }

        return null;
    }

    /// <summary>
    /// 将所有已配置虚拟镜头设置为指定 Priority。
    /// </summary>
    private void SetAllConfiguredCamerasPriority(int priority)
    {
        if (defaultVirtualCamera != null)
        {
            defaultVirtualCamera.Priority = priority;
        }

        for (int i = 0; i < cameraButtonBindings.Length; i++)
        {
            CameraButtonBinding binding = cameraButtonBindings[i];
            if (binding != null && binding.VirtualCamera != null)
            {
                binding.VirtualCamera.Priority = priority;
            }
        }
    }

    /// <summary>
    /// 按当前激活镜头刷新按钮可点击状态。
    /// </summary>
    private void RefreshButtonInteractable()
    {
        if (!manageButtonInteractable)
        {
            return;
        }

        for (int i = 0; i < cameraButtonBindings.Length; i++)
        {
            CameraButtonBinding binding = cameraButtonBindings[i];
            if (binding == null || binding.Button == null)
            {
                continue;
            }

            bool isActiveCameraButton = binding.VirtualCamera == currentVirtualCamera;
            bool keepActiveButtonClickable = activeButtonReturnsToDefaultCamera && isActiveCameraButton;
            binding.Button.interactable = binding.Enabled && (keepActiveButtonClickable || !disableActiveButton || !isActiveCameraButton);
        }
    }

    /// <summary>
    /// 查找主摄像机上的 CinemachineBrain。
    /// </summary>
    private void ResolveBrain()
    {
        if (cinemachineBrain != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }
    }

    /// <summary>
    /// 应用 CinemachineBrain 的默认镜头切换过渡。
    /// </summary>
    private void ApplyDefaultBlend()
    {
        if (!overrideDefaultBlend || cinemachineBrain == null)
        {
            return;
        }

        cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendTime);
    }

    /// <summary>
    /// 在 Inspector 修改参数时修正非法范围。
    /// </summary>
    private void OnValidate()
    {
        EnsureBindingsArray();
        blendTime = Mathf.Max(0f, blendTime);

        if (activePriority <= inactivePriority)
        {
            activePriority = inactivePriority + 1;
        }
    }

    /// <summary>
    /// 保证绑定数组不为空，避免旧组件迁移后运行时报空。
    /// </summary>
    private void EnsureBindingsArray()
    {
        if (cameraButtonBindings == null)
        {
            cameraButtonBindings = Array.Empty<CameraButtonBinding>();
        }
    }
}
