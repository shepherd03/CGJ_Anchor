using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WorldUICameraManager : MonoBehaviour
{
    [Serializable]
    public sealed class CameraButtonBinding
    {
        [SerializeField, Tooltip("区域名称，只用于在 Inspector 里区分配置，不参与逻辑。")]
        private string regionName;

        [SerializeField, Tooltip("玩家点击的世界 UI 按钮。")]
        private Button button;

        [SerializeField, Tooltip("点击该按钮后切换到的 Cinemachine Virtual Camera。")]
        private CinemachineVirtualCamera virtualCamera;

        [SerializeField, Tooltip("是否启用这一组按钮和镜头绑定。关闭后点击不会切换到该镜头。")]
        private bool enabled = true;

        public string RegionName => regionName;
        public Button Button => button;
        public CinemachineVirtualCamera VirtualCamera => virtualCamera;
        public bool Enabled => enabled;
    }

    [Header("Cameras")]
    [SerializeField, Tooltip("主摄像机上的 CinemachineBrain。留空时会自动从 Camera.main 查找。")]
    private CinemachineBrain cinemachineBrain;

    [SerializeField, Tooltip("默认主镜头。按下返回键时会切回这个 Virtual Camera。")]
    private CinemachineVirtualCamera mainVirtualCamera;

    [SerializeField, Tooltip("世界 UI 按钮和区域镜头的映射列表。每一项代表一个可点击区域。")]
    private List<CameraButtonBinding> cameraButtons = new List<CameraButtonBinding>();

    [Header("Priority")]
    [SerializeField, Tooltip("当前激活镜头的 Priority。必须高于未激活镜头。")]
    private int activePriority = 20;

    [SerializeField, Tooltip("未激活镜头的 Priority。数值低于激活镜头即可。")]
    private int inactivePriority = 0;

    [SerializeField, Tooltip("游戏开始时是否强制把镜头切回主镜头，并初始化所有镜头优先级。")]
    private bool initializePrioritiesOnStart = true;

    [Header("Blend")]
    [SerializeField, Tooltip("是否用这里的设置覆盖 CinemachineBrain 的默认过渡效果。")]
    private bool overrideDefaultBlend = true;

    [SerializeField, Tooltip("镜头切换的过渡曲线类型。一般用 EaseInOut，最稳。")]
    private CinemachineBlendDefinition.Style blendStyle = CinemachineBlendDefinition.Style.EaseInOut;

    [SerializeField, Min(0f), Tooltip("镜头切换过渡时间，单位秒。0 表示瞬切。")]
    private float blendTime = 0.8f;

    [Header("Input")]
    [SerializeField, Tooltip("是否允许按返回键切回主镜头。")]
    private bool escapeReturnsToMain = true;

    [SerializeField, Tooltip("切回主镜头的按键。默认是 Esc。")]
    private KeyCode returnKey = KeyCode.Escape;

    [Header("Buttons")]
    [SerializeField, Tooltip("是否自动给每个 Button 注册点击事件。打开后不用在 Button 的 OnClick 里手动绑定。")]
    private bool autoRegisterButtonClicks = true;

    [SerializeField, Tooltip("是否由脚本自动管理按钮是否可点击。")]
    private bool manageButtonInteractable = true;

    [SerializeField, Tooltip("当前已激活镜头对应的按钮是否禁用，防止重复点击同一个区域。")]
    private bool disableActiveButton = true;

    private readonly List<Button> registeredButtons = new List<Button>();
    private readonly List<UnityAction> registeredActions = new List<UnityAction>();
    private CinemachineVirtualCamera currentVirtualCamera;

    private void Awake()
    {
        ResolveBrain();
        ApplyDefaultBlend();
    }

    private void OnEnable()
    {
        if (autoRegisterButtonClicks)
        {
            RegisterButtonListeners();
        }
    }

    private void Start()
    {
        if (initializePrioritiesOnStart)
        {
            ReturnToMainCamera();
        }
        else
        {
            currentVirtualCamera = mainVirtualCamera;
            RefreshButtonInteractable();
        }
    }

    private void Update()
    {
        if (escapeReturnsToMain && Input.GetKeyDown(returnKey))
        {
            ReturnToMainCamera();
        }
    }

    private void OnDisable()
    {
        UnregisterButtonListeners();
    }

    public void SwitchToRegionCamera(int index)
    {
        if (index < 0 || index >= cameraButtons.Count)
        {
            Debug.LogWarning($"{nameof(WorldUICameraManager)} index out of range: {index}", this);
            return;
        }

        CameraButtonBinding binding = cameraButtons[index];
        if (binding == null || !binding.Enabled || binding.VirtualCamera == null)
        {
            Debug.LogWarning($"{nameof(WorldUICameraManager)} has an invalid camera binding at index {index}.", this);
            return;
        }

        SwitchToCamera(binding.VirtualCamera);
    }

    public void SwitchToCamera(CinemachineVirtualCamera targetCamera)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning($"{nameof(WorldUICameraManager)} cannot switch to a null virtual camera.", this);
            return;
        }

        SetAllCamerasPriority(inactivePriority);
        targetCamera.Priority = activePriority;
        currentVirtualCamera = targetCamera;

        RefreshButtonInteractable();
    }

    public void ReturnToMainCamera()
    {
        if (mainVirtualCamera == null)
        {
            Debug.LogWarning($"{nameof(WorldUICameraManager)} needs a main virtual camera.", this);
            return;
        }

        SwitchToCamera(mainVirtualCamera);
    }

    private void RegisterButtonListeners()
    {
        UnregisterButtonListeners();

        for (int i = 0; i < cameraButtons.Count; i++)
        {
            CameraButtonBinding binding = cameraButtons[i];
            if (binding == null || binding.Button == null)
            {
                continue;
            }

            int bindingIndex = i;
            UnityAction action = () => SwitchToRegionCamera(bindingIndex);
            binding.Button.onClick.AddListener(action);

            registeredButtons.Add(binding.Button);
            registeredActions.Add(action);
        }
    }

    private void UnregisterButtonListeners()
    {
        for (int i = 0; i < registeredButtons.Count; i++)
        {
            if (registeredButtons[i] != null)
            {
                registeredButtons[i].onClick.RemoveListener(registeredActions[i]);
            }
        }

        registeredButtons.Clear();
        registeredActions.Clear();
    }

    private void SetAllCamerasPriority(int priority)
    {
        if (mainVirtualCamera != null)
        {
            mainVirtualCamera.Priority = priority;
        }

        for (int i = 0; i < cameraButtons.Count; i++)
        {
            CameraButtonBinding binding = cameraButtons[i];
            if (binding != null && binding.VirtualCamera != null)
            {
                binding.VirtualCamera.Priority = priority;
            }
        }
    }

    private void RefreshButtonInteractable()
    {
        if (!manageButtonInteractable)
        {
            return;
        }

        for (int i = 0; i < cameraButtons.Count; i++)
        {
            CameraButtonBinding binding = cameraButtons[i];
            if (binding == null || binding.Button == null)
            {
                continue;
            }

            bool isActiveCameraButton = binding.VirtualCamera == currentVirtualCamera;
            binding.Button.interactable = binding.Enabled && (!disableActiveButton || !isActiveCameraButton);
        }
    }

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

    private void ApplyDefaultBlend()
    {
        if (!overrideDefaultBlend || cinemachineBrain == null)
        {
            return;
        }

        cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendTime);
    }

    private void OnValidate()
    {
        blendTime = Mathf.Max(0f, blendTime);

        if (activePriority <= inactivePriority)
        {
            activePriority = inactivePriority + 1;
        }
    }
}
