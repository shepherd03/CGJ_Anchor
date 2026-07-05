using Anchor.Character.Attributes;
using Anchor.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YokiFrame;

/// <summary>
/// 持有并转发单个交互点下 Floating UI 的基础行为。
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingUIManager : MonoBehaviour
{
    private const int ManagedButtonCount = 3;
    private const string ActionPointGainTextName = "APGain";

    /// <summary>
    /// Floating UI 前两个选项要消耗的外部行动点类型。
    /// </summary>
    private enum RoomActionPointType
    {
        /// <summary>
        /// 代码房间，对应 GameFlow 的 Program 投点。
        /// </summary>
        Code,

        /// <summary>
        /// 美术房间，对应 GameFlow 的 Art 投点。
        /// </summary>
        Art,

        /// <summary>
        /// 音频房间，对应 GameFlow 的 Audio 投点。
        /// </summary>
        Audio
    }

    [Header("Floating UI")]
    [SerializeField, Tooltip("当前 Floating UI 前两个选项消耗的行动点类型。Code 会映射到 GameFlow 的 Program。")]
    private RoomActionPointType type = RoomActionPointType.Audio;

    [SerializeField, Tooltip("扇形 Floating UI 动画控制器。为空时会从当前物体子级查找。")]
    private FloatingUIFan floatingUIFan;

    [SerializeField, Tooltip("行动点不足反馈控制器。为空时会从当前物体子级查找。")]
    private FloatingUIActionPoints actionPoints;

    [SerializeField, Tooltip("控制当前 Floating UI 开关的按钮组件。为空时会从父级或场景中查找。")]
    private ButtonPrefabSpawner buttonPrefabSpawner;

    // 缓存当前 Floating UI 直接子对象上的三个 Button，前两个接入音效房间行动点逻辑，第三个暂时只持有。
    private readonly Button[] childButtons = new Button[ManagedButtonCount];

    // 缓存三个子按钮的点击回调，保证注册和解绑使用同一个委托实例。
    private readonly UnityAction[] childButtonCallbacks = new UnityAction[ManagedButtonCount];

    // 缓存当前 Floating UI 子级中名为 APGain 的三个行动点文本。
    private readonly TextMeshProUGUI[] actionPointGainTexts = new TextMeshProUGUI[ManagedButtonCount];

    public FloatingUIFan Fan => floatingUIFan;
    public FloatingUIActionPoints ActionPoints => actionPoints;
    public ButtonPrefabSpawner Spawner => buttonPrefabSpawner;

    /// <summary>
    /// 添加组件时自动缓存当前层级下已有的 Floating UI 组件。
    /// </summary>
    private void Reset()
    {
        CacheFloatingUIReferences();
        CacheChildButtons();
        CacheActionPointGainTexts();
        CacheButtonPrefabSpawner();
    }

    /// <summary>
    /// 运行时初始化 Floating UI 组件引用。
    /// </summary>
    private void Awake()
    {
        CacheMissingFloatingUIReferences();
        CacheChildButtons();
        CacheMissingActionPointGainTexts();
        CacheMissingButtonPrefabSpawner();
        EnsureChildButtonCallbacks();
    }

    /// <summary>
    /// 组件启用时注册前两个子按钮的音效行动点消耗事件。
    /// 过期：现在注册的是当前 Type 配置的行动点消耗事件。
    /// 新注释：同时监听玩家属性变化，并刷新三个 APGain 文本为点击后的效果范围。
    /// </summary>
    private void OnEnable()
    {
        RegisterChildButtonClicks();
        RegisterPlayerAttributeChangedEvent();
        RefreshActionPointGainTexts();
    }

    /// <summary>
    /// 组件禁用时移除子按钮事件，避免重复注册。
    /// 新注释：同时注销玩家属性变化事件，避免 inactive UI 继续接收回调。
    /// </summary>
    private void OnDisable()
    {
        UnregisterChildButtonClicks();
        UnregisterPlayerAttributeChangedEvent();
    }

    /// <summary>
    /// Inspector 修改后补齐缺失引用，方便搭房间预制体时少拖字段。
    /// </summary>
    private void OnValidate()
    {
        CacheMissingFloatingUIReferences();
        CacheChildButtons();
        CacheMissingActionPointGainTexts();
    }

    /// <summary>
    /// 重新查找并缓存当前层级下的 Floating UI 组件。
    /// </summary>
    [ContextMenu("Refresh Floating UI References")]
    public void CacheFloatingUIReferences()
    {
        floatingUIFan = GetComponentInChildren<FloatingUIFan>(true);
        actionPoints = GetComponentInChildren<FloatingUIActionPoints>(true);
        CacheChildButtons();
        CacheActionPointGainTexts();
        CacheButtonPrefabSpawner();
    }

    /// <summary>
    /// 只补齐空引用，避免覆盖 Inspector 手动指定的目标。
    /// </summary>
    public void CacheMissingFloatingUIReferences()
    {
        if (floatingUIFan == null)
        {
            floatingUIFan = GetComponentInChildren<FloatingUIFan>(true);
        }

        if (actionPoints == null)
        {
            actionPoints = GetComponentInChildren<FloatingUIActionPoints>(true);
        }
    }

    /// <summary>
    /// 重新查找并缓存控制当前 Floating UI 的 ButtonPrefabSpawner。
    /// </summary>
    public void CacheButtonPrefabSpawner()
    {
        buttonPrefabSpawner = FindButtonPrefabSpawner();
    }

    /// <summary>
    /// 只在开关组件引用为空时补齐，避免覆盖 Inspector 手动指定。
    /// </summary>
    public void CacheMissingButtonPrefabSpawner()
    {
        if (buttonPrefabSpawner == null)
        {
            buttonPrefabSpawner = FindButtonPrefabSpawner();
        }
    }

    /// <summary>
    /// 打开当前交互点绑定的扇形 Floating UI。
    /// </summary>
    public void OpenFan()
    {
        if (floatingUIFan == null)
        {
            Debug.LogWarning($"{nameof(FloatingUIManager)} 缺少 {nameof(FloatingUIFan)} 引用：{name}", this);
            return;
        }

        floatingUIFan.Open();
    }

    /// <summary>
    /// 关闭当前交互点绑定的扇形 Floating UI。
    /// </summary>
    public void CloseFan()
    {
        if (floatingUIFan == null)
        {
            Debug.LogWarning($"{nameof(FloatingUIManager)} 缺少 {nameof(FloatingUIFan)} 引用：{name}", this);
            return;
        }

        floatingUIFan.Close();
    }

    /// <summary>
    /// 兼容旧入口；行动点已经改由 GameFlow 管理，Floating UI 不再重置内部点数。
    /// </summary>
    public void ResetActionPoints()
    {
        // 旧内部行动点已经删除，保留方法避免现有房间交互代码失效。
    }

    /// <summary>
    /// 尝试消耗外部 GameFlow 的音效行动点，失败时显示 Floating UI 提示。
    /// 过期：现在按当前 Type 配置消耗外部 GameFlow 行动点。
    /// </summary>
    public bool TrySpendActionPoints(int amount)
    {
        return TrySpendTypedActionPoints(amount);
    }

    /// <summary>
    /// 尝试消耗外部 GameFlow 的音效行动点。
    /// 过期：现在尝试消耗当前 Type 配置的外部 GameFlow 行动点。
    /// </summary>
    private bool TrySpendTypedActionPoints(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0)
        {
            return true;
        }

        if (!TryGetGameFlowRunner(out GameFlowRunner runner))
        {
            ShowInsufficientActionPointsFeedback();
            return false;
        }

        bool success = TrySpendTypedActionPoints(runner, amount, type);
        if (!success)
        {
            ShowInsufficientActionPointsFeedback();
        }

        return success;
    }

    /// <summary>
    /// 按点数调用 GameFlowRunner 上对应的音效行动点消耗方法。
    /// 过期：现在按 Type 和点数调用 GameFlowRunner 上对应的行动点消耗方法。
    /// </summary>
    private static bool TrySpendTypedActionPoints(GameFlowRunner runner, int amount, RoomActionPointType actionPointType)
    {
        return actionPointType switch
        {
            RoomActionPointType.Code => TrySpendCodeActionPoints(runner, amount),
            RoomActionPointType.Art => TrySpendArtActionPoints(runner, amount),
            RoomActionPointType.Audio => TrySpendAudioActionPoints(runner, amount),
            _ => false
        };
    }

    /// <summary>
    /// 按点数调用 GameFlowRunner 上对应的代码行动点消耗方法。
    /// </summary>
    private static bool TrySpendCodeActionPoints(GameFlowRunner runner, int amount)
    {
        return amount switch
        {
            1 => runner.TrySpendProgramOneActionPoint(),
            2 => runner.TrySpendProgramTwoActionPoints(),
            _ => runner.TryAllocateProgram(amount)
        };
    }

    /// <summary>
    /// 按点数调用 GameFlowRunner 上对应的美术行动点消耗方法。
    /// </summary>
    private static bool TrySpendArtActionPoints(GameFlowRunner runner, int amount)
    {
        return amount switch
        {
            1 => runner.TrySpendArtOneActionPoint(),
            2 => runner.TrySpendArtTwoActionPoints(),
            _ => runner.TryAllocateArt(amount)
        };
    }

    /// <summary>
    /// 按点数调用 GameFlowRunner 上对应的音频行动点消耗方法。
    /// </summary>
    private static bool TrySpendAudioActionPoints(GameFlowRunner runner, int amount)
    {
        return amount switch
        {
            1 => runner.TrySpendAudioOneActionPoint(),
            2 => runner.TrySpendAudioTwoActionPoints(),
            _ => runner.TryAllocateAudio(amount)
        };
    }

    /// <summary>
    /// 缓存当前物体子级中名为 APGain 的三个 TextMeshProUGUI 文本。
    /// 过期：全层级顺序查找会让 APGain 和按钮点击效果错位。
    /// 新注释：按前三个直接子按钮分别查找其子级 APGain 文本，保证文本对应同下标按钮。
    /// </summary>
    private void CacheActionPointGainTexts()
    {
        for (int i = 0; i < actionPointGainTexts.Length; i++)
        {
            actionPointGainTexts[i] = null;
        }

        for (int i = 0; i < actionPointGainTexts.Length; i++)
        {
            Transform child = transform.childCount > i ? transform.GetChild(i) : null;
            actionPointGainTexts[i] = child != null ? FindActionPointGainText(child) : null;
        }

        FillMissingActionPointGainTextsFromWholeHierarchy();
    }

    /// <summary>
    /// 在指定按钮层级下查找名为 APGain 的 TextMeshProUGUI。
    /// </summary>
    private static TextMeshProUGUI FindActionPointGainText(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == ActionPointGainTextName)
            {
                return texts[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 兼容旧层级：如果 APGain 不在对应按钮子级下，再按原全层级顺序补齐空位。
    /// </summary>
    private void FillMissingActionPointGainTextsFromWholeHierarchy()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        int missingIndex = 0;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null || texts[i].name != ActionPointGainTextName)
            {
                continue;
            }

            if (IsActionPointGainTextCached(texts[i]))
            {
                continue;
            }

            while (missingIndex < actionPointGainTexts.Length && actionPointGainTexts[missingIndex] != null)
            {
                missingIndex++;
            }

            if (missingIndex >= actionPointGainTexts.Length)
            {
                return;
            }

            actionPointGainTexts[missingIndex] = texts[i];
        }
    }

    /// <summary>
    /// 判断 APGain 文本是否已经绑定到某个按钮槽位。
    /// </summary>
    private bool IsActionPointGainTextCached(TextMeshProUGUI text)
    {
        for (int i = 0; i < actionPointGainTexts.Length; i++)
        {
            if (actionPointGainTexts[i] == text)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// APGain 文本引用为空时重新查找，兼容运行时动态启用的层级。
    /// </summary>
    private void CacheMissingActionPointGainTexts()
    {
        for (int i = 0; i < actionPointGainTexts.Length; i++)
        {
            if (actionPointGainTexts[i] == null)
            {
                CacheActionPointGainTexts();
                return;
            }
        }
    }

    /// <summary>
    /// 注册玩家行动点变化事件，用于消费行动点后同步刷新 APGain 文本。
    /// 过期：APGain 现在展示点击后的效果范围，不再展示剩余行动点。
    /// 新注释：注册玩家属性变化事件，用于属性边界或临时加成变化后刷新 APGain 文本。
    /// </summary>
    private void RegisterPlayerAttributeChangedEvent()
    {
        EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnCharacterAttributeChanged);
        EventKit.Type.Register<CharacterAttributeChangedEvent>(OnCharacterAttributeChanged);
    }

    /// <summary>
    /// 注销玩家行动点变化事件。
    /// 过期：APGain 现在展示点击后的效果范围，不再展示剩余行动点。
    /// 新注释：注销玩家属性变化事件。
    /// </summary>
    private void UnregisterPlayerAttributeChangedEvent()
    {
        EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnCharacterAttributeChanged);
    }

    /// <summary>
    /// 玩家当前周行动点变化时，同步刷新三个 APGain 文本。
    /// 过期：APGain 现在展示点击后的效果范围，不再展示剩余行动点。
    /// 新注释：当前玩家属性变化时，同步刷新三个 APGain 文本，保证临时加成变化后 UI 立刻更新。
    /// </summary>
    private void OnCharacterAttributeChanged(CharacterAttributeChangedEvent attributeEvent)
    {
        if (!IsCurrentPlayerAttributeSet(attributeEvent.AttributeSet))
        {
            return;
        }

        RefreshActionPointGainTexts();
    }

    /// <summary>
    /// 用当前流程黑板里的剩余行动点刷新三个 APGain 文本。
    /// 过期：APGain 现在展示点击后的效果范围，不再展示剩余行动点。
    /// 新注释：用当前流程黑板里的房间行动效果边界刷新三个 APGain 文本。
    /// </summary>
    private void RefreshActionPointGainTexts()
    {
        if (TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
        {
            RefreshActionPointGainTexts(blackboard);
            return;
        }

        ClearActionPointGainTexts();
    }

    /// <summary>
    /// 用指定剩余行动点数刷新三个 APGain 文本。
    /// 过期：APGain 现在展示点击后的效果范围，不再展示剩余行动点。
    /// 新注释：用指定流程黑板刷新三个 APGain 文本，第一个按钮预览 1AP 效果，第二个按钮预览 2AP 效果。
    /// </summary>
    private void RefreshActionPointGainTexts(GameFlowBlackboard blackboard)
    {
        CacheMissingActionPointGainTexts();

        for (int i = 0; i < actionPointGainTexts.Length; i++)
        {
            if (actionPointGainTexts[i] != null)
            {
                actionPointGainTexts[i].text = GetActionPointGainText(blackboard, i);
            }
        }
    }

    /// <summary>
    /// 清空所有 APGain 文本，避免没有流程数据时显示旧值。
    /// </summary>
    private void ClearActionPointGainTexts()
    {
        CacheMissingActionPointGainTexts();

        for (int i = 0; i < actionPointGainTexts.Length; i++)
        {
            if (actionPointGainTexts[i] != null)
            {
                actionPointGainTexts[i].text = string.Empty;
            }
        }
    }

    /// <summary>
    /// 获取指定按钮点击后会执行的房间行动效果范围文本。
    /// </summary>
    private string GetActionPointGainText(GameFlowBlackboard blackboard, int buttonIndex)
    {
        if (blackboard == null || !TryGetActionPointsForButton(buttonIndex, out int points))
        {
            return string.Empty;
        }

        GameDevelopmentTrack track = GetDevelopmentTrack(type);
        return blackboard.TryGetRoomActionEffectRange(track, points, out int minValue, out int maxValue)
            ? FormatEffectRange(minValue, maxValue)
            : string.Empty;
    }

    /// <summary>
    /// 获取 Floating UI 子按钮对应的消耗点数；第三个按钮当前没有行动效果。
    /// </summary>
    private static bool TryGetActionPointsForButton(int buttonIndex, out int points)
    {
        points = buttonIndex switch
        {
            0 => 1,
            1 => 2,
            _ => 0
        };

        return points > 0;
    }

    /// <summary>
    /// 将效果范围格式化为 UI 上显示的带符号边界值。
    /// </summary>
    private static string FormatEffectRange(int minValue, int maxValue)
    {
        return minValue == maxValue
            ? FormatSignedValue(minValue)
            : $"{FormatSignedValue(minValue)}~{FormatSignedValue(maxValue)}";
    }

    /// <summary>
    /// 格式化单个带符号效果值。
    /// </summary>
    private static string FormatSignedValue(int value)
    {
        return value.ToString("+0;-0;0");
    }

    /// <summary>
    /// 获取当前流程黑板。
    /// </summary>
    private static bool TryGetCurrentBlackboard(out GameFlowBlackboard blackboard)
    {
        blackboard = null;

        GameFlowRunner runner = GameFlowRunner.Instance;
        if (runner == null || runner.Controller == null)
        {
            return false;
        }

        blackboard = runner.Controller.Blackboard;
        return true;
    }

    /// <summary>
    /// 判断属性变化是否来自当前流程玩家属性集合。
    /// </summary>
    private static bool IsCurrentPlayerAttributeSet(CharacterAttributeSet attributeSet)
    {
        GameFlowRunner runner = GameFlowRunner.Instance;
        return runner != null
            && runner.Controller != null
            && attributeSet == runner.Controller.Blackboard.PlayerAttributes;
    }

    /// <summary>
    /// 缓存当前物体前三个直接子对象上的 Button。
    /// </summary>
    private void CacheChildButtons()
    {
        for (int i = 0; i < childButtons.Length; i++)
        {
            Transform child = transform.childCount > i ? transform.GetChild(i) : null;
            childButtons[i] = child != null ? child.GetComponent<Button>() : null;
        }
    }

    /// <summary>
    /// 注册音效房间行动点按钮点击事件。
    /// 过期：现在注册当前 Type 配置的房间行动点按钮点击事件。
    /// </summary>
    private void RegisterChildButtonClicks()
    {
        CacheChildButtons();
        CacheMissingButtonPrefabSpawner();
        EnsureChildButtonCallbacks();

        for (int i = 0; i < childButtonCallbacks.Length; i++)
        {
            RegisterButtonClick(i, childButtonCallbacks[i]);
        }
    }

    /// <summary>
    /// 移除 Floating UI 子按钮点击事件。
    /// </summary>
    private void UnregisterChildButtonClicks()
    {
        EnsureChildButtonCallbacks();

        for (int i = 0; i < childButtonCallbacks.Length; i++)
        {
            UnregisterButtonClick(i, childButtonCallbacks[i]);
        }
    }

    /// <summary>
    /// 给指定子按钮注册点击事件，注册前先移除同一事件防止重复绑定。
    /// </summary>
    private void RegisterButtonClick(int buttonIndex, UnityAction action)
    {
        if (!TryGetChildButton(buttonIndex, out Button button))
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 从指定子按钮移除点击事件。
    /// </summary>
    private void UnregisterButtonClick(int buttonIndex, UnityAction action)
    {
        if (!TryGetChildButton(buttonIndex, out Button button))
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    /// <summary>
    /// 获取指定下标的子按钮。
    /// </summary>
    private bool TryGetChildButton(int buttonIndex, out Button button)
    {
        button = null;

        if (buttonIndex < 0 || buttonIndex >= childButtons.Length)
        {
            return false;
        }

        button = childButtons[buttonIndex];
        return button != null;
    }

    /// <summary>
    /// 确保三个子按钮都有稳定的点击回调。
    /// </summary>
    private void EnsureChildButtonCallbacks()
    {
        childButtonCallbacks[0] ??= OnFirstChildButtonClicked;
        childButtonCallbacks[1] ??= OnSecondChildButtonClicked;
        childButtonCallbacks[2] ??= OnThirdChildButtonClicked;
    }

    /// <summary>
    /// 第一个子按钮：尝试消耗 1 点音效行动点，然后关闭 Floating UI。
    /// 过期：现在尝试消耗当前 Type 配置的 1 点行动点，然后关闭 Floating UI。
    /// </summary>
    private void OnFirstChildButtonClicked()
    {
        bool actionApplied = TrySpendTypedActionPoints(1);
        CloseFloatingUIFromSpawner();
        LockSpawnerForCurrentWeekIfActionApplied(actionApplied);
    }

    /// <summary>
    /// 第二个子按钮：尝试消耗 2 点音效行动点，然后关闭 Floating UI。
    /// 过期：现在尝试消耗当前 Type 配置的 2 点行动点，然后关闭 Floating UI。
    /// </summary>
    private void OnSecondChildButtonClicked()
    {
        bool actionApplied = TrySpendTypedActionPoints(2);
        CloseFloatingUIFromSpawner();
        LockSpawnerForCurrentWeekIfActionApplied(actionApplied);
    }

    /// <summary>
    /// 第三个子按钮：暂时只负责关闭 Floating UI。
    /// </summary>
    private void OnThirdChildButtonClicked()
    {
        CloseFloatingUIFromSpawner();
    }

    /// <summary>
    /// 调用 ButtonPrefabSpawner 的完整按钮点击链，复用原按钮再次点击的全部效果。
    /// </summary>
    private void CloseFloatingUIFromSpawner()
    {
        CacheMissingButtonPrefabSpawner();

        if (buttonPrefabSpawner != null)
        {
            buttonPrefabSpawner.InvokeToggleButtonClick();
            return;
        }

        CloseFan();
    }

    /// <summary>
    /// 房间行动成功后锁定入口按钮，避免玩家本周重复打开同一个 Floating UI。
    /// </summary>
    private void LockSpawnerForCurrentWeekIfActionApplied(bool actionApplied)
    {
        if (!actionApplied)
        {
            return;
        }

        if (TryGetGameFlowRunner(out GameFlowRunner runner) &&
            HasRemainingRoomOperationCount(runner, type))
        {
            return;
        }

        CacheMissingButtonPrefabSpawner();

        if (buttonPrefabSpawner != null)
        {
            buttonPrefabSpawner.LockForCurrentWeek();
        }
    }

    /// <summary>
    /// 当前 Type 对应的房间本周还有剩余操作次数时，入口不锁定。
    /// </summary>
    private static bool HasRemainingRoomOperationCount(GameFlowRunner runner, RoomActionPointType actionPointType)
    {
        if (runner == null || runner.Controller == null)
        {
            return false;
        }

        return runner.Controller.Blackboard.HasRoomOperationCount(GetDevelopmentTrack(actionPointType));
    }

    /// <summary>
    /// 将 Floating UI 的房间类型映射到 GameFlow 的开发轨道。
    /// </summary>
    private static GameDevelopmentTrack GetDevelopmentTrack(RoomActionPointType actionPointType)
    {
        return actionPointType switch
        {
            RoomActionPointType.Code => GameDevelopmentTrack.Program,
            RoomActionPointType.Art => GameDevelopmentTrack.Art,
            RoomActionPointType.Audio => GameDevelopmentTrack.Audio,
            _ => GameDevelopmentTrack.Audio
        };
    }

    /// <summary>
    /// 显示外部行动点不足反馈。
    /// </summary>
    private void ShowInsufficientActionPointsFeedback()
    {
        CacheMissingFloatingUIReferences();

        if (actionPoints != null)
        {
            actionPoints.ShowInsufficientWarning();
        }
    }

    /// <summary>
    /// 获取当前场景的游戏流程入口。
    /// </summary>
    private bool TryGetGameFlowRunner(out GameFlowRunner runner)
    {
        runner = GameFlowRunner.Instance;

        if (runner != null)
        {
            return true;
        }

        Debug.LogWarning($"{nameof(FloatingUIManager)} 找不到 {nameof(GameFlowRunner)}，无法消耗 {type} 房间行动点：{name}", this);
        return false;
    }

    /// <summary>
    /// 查找控制当前 Floating UI 所在层级的 ButtonPrefabSpawner。
    /// </summary>
    private ButtonPrefabSpawner FindButtonPrefabSpawner()
    {
        ButtonPrefabSpawner parentSpawner = GetComponentInParent<ButtonPrefabSpawner>(true);
        if (parentSpawner != null && parentSpawner.ControlsTarget(transform))
        {
            return parentSpawner;
        }

        ButtonPrefabSpawner[] spawners = FindObjectsOfType<ButtonPrefabSpawner>(true);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null && spawners[i].ControlsTarget(transform))
            {
                return spawners[i];
            }
        }

        return null;
    }
}
