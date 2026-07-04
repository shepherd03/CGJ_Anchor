# 游戏流程系统接入指南

这套流程系统负责驱动游戏的月循环、周循环、行动点投入、周结算、月结算和结局选择。

核心原则：

- 流程状态只表示游戏走到哪一步。
- 玩家数值统一放在 `CharacterAttributeSet`。
- Buff、事件、商店、UI 想改玩家数值时，直接改玩家属性。
- 月/周/结局公式集中在 `GameFlowResolveService`。

## 目录结构

主要代码位置：

```text
Assets/Scripts/GameFlow
Assets/Scripts/Character
Assets/Scripts/Character/Attributes
```

关键文件：

```text
GameFlowRunner.cs                  Unity 场景入口组件
GameFlowController.cs              流程控制门面
GameFlowBlackboard.cs              流程运行时黑板，持有玩家和流程上下文
GameFlowResolveService.cs          周结算、月结算、结局公式
GameFlowState.cs                   主流程状态枚举
GameDevelopmentTrack.cs            周行动投点方向
MonthSettlementType.cs             月结算类型
CharacterAttributeIds.cs           玩家核心属性表格 ID 常量
CharacterAttributeCatalog.cs       玩家属性配置表运行时索引
CharacterAttributeSet.cs           玩家属性容器
GamePlayer.cs                      玩家对象，只持有属性容器
```

## 场景接入

1. 在场景里创建一个空物体，例如 `GameFlowRunner`。
2. 挂载 `GameFlowRunner` 组件。
3. 按需要配置 Inspector 参数。

Inspector 参数：

```text
Start On Awake                 是否进入场景后自动开始新游戏
Auto Advance Interactive States 是否自动跳过商店、行动、结算等待阶段
Log Events                     是否在 Console 打印流程和属性变化
Total Months                   总月份数
Weeks Per Month                每月周数
```

推荐测试配置：

```text
Start On Awake = true
Auto Advance Interactive States = true
Log Events = true
```

这样运行场景后，Console 会直接看到完整流程、周结算、月结算、属性变化和最终结局。

## 手动驱动流程

如果要用 UI 控制流程，不勾选 `Auto Advance Interactive States`。

可以调用 `GameFlowRunner` 上的方法：

```csharp
runner.StartNewGame();
runner.ConfirmBudgetShop();
runner.TryAllocateProgram(1);
runner.TryAllocateArt(1);
runner.TryAllocateDesign(1);
runner.TryAllocateTesting(1);
runner.TryAllocateMarketing(1);
runner.FinishWeekAction();
runner.ContinueFlow();
```

常见 UI 对应关系：

```text
开始游戏按钮             StartNewGame
月初商店确认按钮         ConfirmBudgetShop
程序房间投点按钮         TryAllocateProgram
美术房间投点按钮         TryAllocateArt
设计房间投点按钮         TryAllocateDesign
测试房间投点按钮         TryAllocateTesting
宣传房间投点按钮         TryAllocateMarketing
本周结束按钮             FinishWeekAction
周结算/月结算继续按钮    ContinueFlow
```

`TryAllocateXxx` 返回 `false` 表示当前不是周行动阶段，或行动力不足。

## 玩家属性

玩家属性定义在 Luban Excel 表：

```text
Config/Luban/Datas/#game.playerAttribute.xlsx
```

表字段：

```text
id            属性 ID，其他表通过该 ID 修改属性
displayName   中文显示名
defaultValue  新游戏默认值
comment       说明
```

运行时通过 `CharacterAttributeSet` 读写属性值。`CharacterAttributeSet` 不再使用 C# 枚举作为键，而是直接使用表里的 `id`。所有玩家属性值都是整数。

当前玩家属性：

```text
1001 基础周行动力，影响每周刷新出来的行动力
1002 当前周剩余行动力，投点会消耗它
1003 月发金币，月开始时加到金币
1004 月愿望单增长，月结算时参与计算
1005 Bug 值
1006 画面
1007 氛围
1008 当前金币，唯一货币
1009 愿望单数量
1010 质量分
```

读写示例：

```csharp
var blackboard = runner.Controller.Blackboard;
var attributes = runner.Controller.Blackboard.PlayerAttributes;

var coins = attributes.Get(CharacterAttributeIds.Coins);
attributes.Add(CharacterAttributeIds.Coins, 100);
attributes.Add(CharacterAttributeIds.Bug, -5);
attributes.Add(CharacterAttributeIds.BaseWeeklyActionPower, 1);
attributes.Add(CharacterAttributeIds.WeeklyActionPower, 2);
attributes.Add(CharacterAttributeIds.MonthlyCoinIncome, 200);
attributes.Add(CharacterAttributeIds.MonthlyWishlistGrowth, 30);
```

如果其他配置表要修改玩家属性，直接配置目标属性 `id` 和变化值即可：

```text
attributeId  delta
1008         100
1002         2
1005         -5
```

读取到配置后，对当前玩家属性执行 `PlayerAttributes.Add(attributeId, delta)`。

在 Buff 和事件表里，这种修改会写成二维整数数组：

```text
[[属性ID,整数值],[属性ID,整数值]]
```

例如：

```text
[[1008,100],[1002,2],[1005,-5]]
```

注意：

- 改 `BaseWeeklyActionPower` 会影响后续每周刷新的行动力。
- 改 `WeeklyActionPower` 只影响当前周剩余行动力。
- 改 `MonthlyCoinIncome` 会影响后续月初发钱。
- 改 `MonthlyWishlistGrowth` 会影响后续月结算愿望单增长。
- `Quality` 当前更像综合结果，建议优先通过公式产生，少直接硬改。

## Buff 和事件接入

Buff 或事件只需要拿到当前黑板，然后修改玩家属性。

示例：当前周立刻获得 2 点行动力。

```csharp
blackboard.PlayerAttributes.Add(CharacterAttributeIds.WeeklyActionPower, 2);
```

示例：以后每周基础行动力 +1。

```csharp
blackboard.PlayerAttributes.Add(CharacterAttributeIds.BaseWeeklyActionPower, 1);
```

示例：每月发钱 +200。

```csharp
blackboard.PlayerAttributes.Add(CharacterAttributeIds.MonthlyCoinIncome, 200);
```

示例：本月结算基础愿望单增长 +50。

```csharp
blackboard.PlayerAttributes.Add(CharacterAttributeIds.MonthlyWishlistGrowth, 50);
```

示例：减少 Bug。

```csharp
blackboard.PlayerAttributes.Set(
    CharacterAttributeIds.Bug,
    Math.Max(0, blackboard.BugScore - 5));
```

如果是一次性事件，直接改属性即可。

如果是持续效果，建议后续单独做一层 Buff Runtime，用于记录持续时间、过期时机和回滚逻辑。

### 配表字段

事件表：

```text
Config/Luban/Datas/#game.gameEvent.xlsx
```

核心效果字段：

```text
yesEffects           选择 Y 后应用的属性修改，格式 [[属性ID,值]]
noEffects            选择 N 后应用的属性修改，格式 [[属性ID,值]]
triggerConditions    触发条件，格式 [[属性ID,阈值]]
```

Buff 表：

```text
Config/Luban/Datas/#game.buff.xlsx
```

核心效果字段：

```text
effects              Buff 生效时应用的属性修改，格式 [[属性ID,值]]
triggerConditions    Buff 触发条件，格式 [[属性ID,阈值]]
```

这些字段在 Luban 里类型为 `array,array,int`，生成到 C# 后是 `int[][]`。每个内部数组必须刚好两项：第 1 项是玩家属性 ID，第 2 项是整数值。

## UI 监听

流程系统通过 `EventKit.Type` 广播事件。

监听流程状态：

```csharp
EventKit.Type.Register<GameFlowStateChangedEvent>(OnStateChanged);

private void OnStateChanged(GameFlowStateChangedEvent e)
{
    var state = e.State;
    var blackboard = e.Blackboard;
}
```

监听周结算：

```csharp
EventKit.Type.Register<WeekResolvedEvent>(OnWeekResolved);

private void OnWeekResolved(WeekResolvedEvent e)
{
    Debug.Log(e.Result.Summary);
}
```

监听月结算：

```csharp
EventKit.Type.Register<MonthSettledEvent>(OnMonthSettled);
```

监听结局：

```csharp
EventKit.Type.Register<GameEndingSelectedEvent>(OnEndingSelected);
```

监听玩家属性变化：

```csharp
EventKit.Type.Register<CharacterAttributeChangedEvent>(OnAttributeChanged);

private void OnAttributeChanged(CharacterAttributeChangedEvent e)
{
    Debug.Log($"{e.AttributeId}: {e.PreviousValue} -> {e.CurrentValue}");
}
```

组件销毁或禁用时要配对注销：

```csharp
EventKit.Type.UnRegister<GameFlowStateChangedEvent>(OnStateChanged);
EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnAttributeChanged);
```

## 内容扩展

### 修改月份结构

默认月份由 `GameFlowDefinitionProvider` 生成。

当前规则：

```text
第 1 月：PV 发布
第 2 月：内测
最后 1 月：正式发布
其他月份：公测
```

如果之后接配置表，可以把 `GameFlowDefinitionProvider.BuildDefaultMonths` 改成从 Luban 表读取。

`MonthDefinition` 只建议放流程定义：

```text
MonthIndex
DisplayName
SettlementType
WeekCount
```

不要把玩家会成长、会被 Buff 改的数值放进 `MonthDefinition`。

### 修改结算公式

周结算、月结算和结局判断都在：

```text
GameFlowResolveService.cs
```

当前周结算会根据投点方向计算：

```text
Program   影响质量，也会增加 Bug
Art       影响画面和质量
Design    影响氛围和质量，也会增加 Bug
Testing   降低 Bug
Marketing 增加愿望单
```

当前月结算会根据：

```text
MonthlyWishlistGrowth
Quality
Bug
MonthSettlementType
```

计算愿望单和金币变化。

### 添加新玩家属性

1. 在 `Config/Luban/Datas/#game.playerAttribute.xlsx` 添加一行属性。
2. 填好唯一 `id`、`displayName` 和 `defaultValue`。
3. 执行 `Config/Luban/export_tables.ps1` 或 `Tools/gen_luban.ps1` 重新生成表数据。
4. 如果流程代码需要频繁使用该属性，可在 `GameFlowBlackboard` 缓存对应 ID，或在具体系统里直接读取配置表传来的属性 ID。
5. Buff、事件或结算公式通过属性 ID 读写 `PlayerAttributes`。

## 测试建议

### 快速自动测试

1. 场景中挂 `GameFlowRunner`。
2. 勾选 `Start On Awake`。
3. 勾选 `Auto Advance Interactive States`。
4. 勾选 `Log Events`。
5. 运行场景，看 Console。

应该看到：

```text
状态变化
属性变化
周结算日志
月结算日志
最终结局日志
```

### 手动流程测试

不勾选 `Auto Advance Interactive States`，运行后在组件右键菜单依次执行：

```text
开始新游戏
确认月初商店
结束本周行动
继续流程
```

也可以通过 UI 按钮调用 `TryAllocateProgram` 等投点方法。

### 属性修改测试

在运行中拿到：

```csharp
var blackboard = runner.Controller.Blackboard;
```

然后直接修改：

```csharp
blackboard.PlayerAttributes.Add(CharacterAttributeIds.Coins, 999);
blackboard.PlayerAttributes.Add(CharacterAttributeIds.BaseWeeklyActionPower, 1);
blackboard.PlayerAttributes.Add(CharacterAttributeIds.MonthlyWishlistGrowth, 100);
```

如果 `Log Events` 打开，Console 应该能看到属性变化日志。

## 设计边界

应该放玩家属性：

```text
会成长
会被 Buff/事件/商店修改
需要 UI 显示
需要存档
影响多个流程阶段
```

应该放流程定义：

```text
第几月
每月几周
这个月是什么结算类型
流程状态顺序
```

应该放公式配置或服务：

```text
投点换算比例
金币消耗
愿望单倍率
结局阈值
Bug 惩罚系数
```

后续如果公式参数也需要被 Buff 修改，再考虑把它们升级成玩家属性或独立的修正器系统。
