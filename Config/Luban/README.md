# Luban 配表工程

这个目录是 `Anchor` 项目的本地 `Luban` 工程根目录。

## 目录约定

- `Defines/`：类型定义、mapper 和字段规则
- `Datas/`：Excel / JSON / XML 等源表
- `luban.conf`：Luban 总配置

## 当前初始化内容

- 已放入官方最小模板需要的系统表：
  - `Datas/__tables__.xlsx`
  - `Datas/__beans__.xlsx`
  - `Datas/__enums__.xlsx`
- 已放入一个最小示例表：`Datas/#demo.item.xlsx`
- 已放入基础定义文件：`Defines/builtin.xml`

## 生成输出

- 配置代码：`Assets/Scripts/Generated/Luban`
- 二进制数据：`Assets/Resources/Config/Luban/Bin`

当前输出目录这样设计，是为了先让项目在 `Unity + YokiFrame` 下快速跑通。
后续如果你们把资源统一切到 `ResKit` 的其他 Provider，例如 Addressables、AssetBundle 或远端资源，
只需要调整生成脚本里的 `outputDataDir` 和项目侧加载入口，不需要重做表工程。

## 常用命令

在仓库根目录执行：

```powershell
.\Tools\gen_luban.ps1
```

或：

```powershell
.\Tools\gen_luban.cmd
```

## 运行时入口

生成后的表数据通过 `YokiFrame.ResKit` 读取，项目侧统一入口是：

```csharp
var tables = Anchor.Config.GameConfigs.Tables;
var firstItem = tables.Tbitem.DataList[0];
```

不要在业务代码里直接拼 `Resources` 路径或手动创建 `Luban.ByteBuf`。如果后续切换 YooAsset、Addressables 或其他资源 Provider，优先只调整 `GameConfigs` 这一层。

## 下一步建议

1. 打开 `#demo.item.xlsx` 看一遍最小表结构
2. 复制示例表，开始建你们自己的业务表
3. 等业务字段稳定后，再补一层 `YokiFrame` 风格的配置加载门面
