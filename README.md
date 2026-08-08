# Days of Food

一个 RimWorld 1.6 模组：让烹饪账单按"天数"自动维持食物产量。

## 功能

- 在食物配方账单的重复模式菜单中新增三个选项：**一天份 / 三天份 / 五天份**（仅当配方产物是可食用且营养值大于 0 的食物时显示）
- 目标数量 = 本地图全体需要进食者（殖民者含奴隶 + 我方囚犯）的每日总营养需求 × 天数 ÷ 单份食物营养值
- 每日总需求直接读取小人的饥饿下降速率，**已自动包含**肠道蠕虫等饥饿速率系数影响
- **每天自动刷新一次**，按地图独立计算，不跨图共享
- 玩家手动改数量没关系，下次刷新会覆盖回计算值；切回原版模式即自动关闭追踪
- 存档安全：可随时添加/移除

## 兼容性

- 底层使用原版 `TargetCount` 模式，不新增 repeatModeDef —— 计数、暂停、进度显示完全走原版逻辑
- 重复模式菜单与 **Everybody Gets One**、**Compositable Loadouts**、**Ingredient Threshold** 自动兼容（检测到这些模组时，它们的自定义模式会自动保留在菜单中）
- 按钮/行内标签兼容 **Nice Bill Tab**（软依赖，未安装时自动跳过）

## 构建

```
cd Source
dotnet build
```

需要 .NET SDK（8.0+）。通过 `Krafs.Rimworld.Ref` 引用程序集编译，**无需本机安装 RimWorld**；产物输出到 `1.6/Assemblies/DaysOfFood.dll`。

## 依赖

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)

## 致谢

架构与兼容方案参考 [Hauler's Dream](https://github.com/Refzlund)（Refzlund）。

## License

MIT

---

# Days of Food (English)

A RimWorld 1.6 mod that keeps N days of food in stock, automatically.

## Features

- Adds **1 day / 3 days / 5 days** repeat modes to food bills — shown only for recipes whose product is edible food with nutrition > 0
- Target count = this map's total daily nutrition need × days ÷ nutrition per item
- Daily need is read from each pawn's food-fall rate, so hunger-rate effects like gut worms are included automatically
- Refreshed once per in-game day, computed per map, never shared across maps
- Manual edits are overwritten on the next refresh; switching back to a vanilla mode untracks the bill
- Save-safe: add or remove at any time

## Compatibility

- Built on vanilla `TargetCount`; no custom repeat-mode defs, so counting and gating stay 100% vanilla
- Custom repeat modes from **Everybody Gets One**, **Compositable Loadouts** and **Ingredient Threshold** are automatically kept in the menu when those mods are present
- **Nice Bill Tab** is supported as a soft dependency

## Build

```
cd Source
dotnet build
```

Requires a .NET SDK (8.0+). Compiles against the `Krafs.Rimworld.Ref` reference assemblies — no local RimWorld install needed. The build outputs `1.6/Assemblies/DaysOfFood.dll`.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)

## Credits

Architecture and compatibility approach adapted from [Hauler's Dream](https://github.com/Refzlund) by Refzlund.

## License

MIT
