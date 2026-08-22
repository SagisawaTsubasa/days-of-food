# Days of Food

一个 RimWorld 1.6 模组：让食物账单按"天数"自动维持产量。

## 这是什么

在账单详情窗口"数量达到时暂停"下方会多出一个"自动维持数量"开关。勾上它，填个天数，模组就会按殖民地当前的吃饭人口自动算出该维持多少食物，并且每天自动更新一次。人多了自动加量，人少了自动减量，不用再手动估算该做多少份饭。

目标数量的算法：本地图全体进食者（自由殖民者含奴隶，加上我方囚犯）的每日总营养需求，乘以天数，除以单份食物的营养值。每日需求直接读取小人的饥饿下降速率，所以肠道蠕虫、胃部感染这类饥饿加成自动包含在内，不需要任何额外设置。每张地图独立计算。

## 细节

只有产物是食物的账单会显示这个开关，"无限制"模式下不显示。手动改过目标数量也没关系，下次每日刷新会覆盖回计算值。切走"维持X个"模式会立刻停止追踪，复制粘贴账单会保留自动状态。存档安全，随时可以添加或移除。

自动维持还能叠加"到量暂停"：设维持 5 天、直到 2 天，库存达到 5 天量就暂停生产，跌破 2 天量再恢复，不用手动反复开关账单。暂停天数会自动钳制在维持天数之下，复制粘贴账单同样携带。

## 兼容性

模组不修改任何账单下拉菜单，入口是内嵌在原版界面里的一行原生控件，因此和 haulers-dream、Everybody Gets One、Compositable Loadouts、Ingredient Threshold 等修改账单菜单的模组零冲突。它还可以和 haulers-dream 的批量烹饪叠加使用：这个模组负责每天算出补货目标，HD 按目标批量生产。

## 依赖

RimWorld 1.6 和 Harmony。

---

# Days of Food (English)

A RimWorld 1.6 mod that keeps N days of food in stock, automatically.

## What it does

A new "Auto-maintain stock" toggle appears in the bill details dialog, right under "Pause when satisfied". Turn it on, set a number of days, and the mod computes how much food your colony needs based on its current eating population, then refreshes that target automatically once per day. More mouths means more meals, fewer means less waste — no more guessing how many meals to queue.

The target is the total daily nutrition need of everyone who eats on this map (free colonists including slaves, plus your prisoners), multiplied by the number of days, divided by the nutrition per item. Daily need is read from each pawn's food-fall rate, so hunger-rate effects like gut worms and stomach infections are already included. Each map is computed independently.

## Details

The toggle only shows on bills whose product is edible food, and never in "Do forever" mode. Manual edits to the target are fine — the next daily refresh simply overwrites them. Switching the bill away from "Do until you have X" stops tracking immediately, and copying a bill keeps its auto state. Safe to add or remove from a save at any time.

Auto-maintain can also drive a "pause when stocked" threshold: set 5 days maintain and 2 days resume, and crafting pauses once 5 days are stocked, resuming only when the stock drops below 2 days — no more toggling bills by hand. The pause days are clamped below the maintained days, and the setting is carried by bill copy/paste.

## Compatibility

The mod never touches bill dropdown menus; its entry point is a native-looking row inside the vanilla dialog, so it cannot conflict with haulers-dream, Everybody Gets One, Compositable Loadouts, Ingredient Threshold, or any other mod that edits bill menus. It also composes nicely with haulers-dream's batch crafting: this mod computes the restock target each day, and HD batch-produces toward it.

## Requirements

RimWorld 1.6 and Harmony.
