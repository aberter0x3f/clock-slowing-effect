#import "@preview/touying:0.6.1": *
#import themes.university: *
#import "@preview/cetz:0.3.1"
#import "@preview/fletcher:0.5.8" as fletcher: edge, node

#let fonts = (
  mono: "IBM Plex Mono",
  serif: "IBM Plex Serif",
  sans: "IBM Plex Sans",
  math: "IBM Plex Math",
  cjk-serif: "Source Han Serif SC",
  cjk-sans: "Sarasa UI SC",
)

#let cetz-canvas = touying-reducer.with(
  reduce: cetz.canvas,
  cover: cetz.draw.hide.with(bounds: true),
)

#let fletcher-diagram = touying-reducer.with(
  reduce: fletcher.diagram,
  cover: fletcher.hide,
)


#show: university-theme.with(
  aspect-ratio: "16-9",
  align: horizon,
  config-info(
    title: [模拟宇宙：时空舞台],
    subtitle: [4D Roguelite 弹幕射击游戏 -- 复赛展示],
    author: [上海交通大学 - Untitled],
    date: datetime(year: 2026, month: 1, day: 11),
    institution: [米哈游首届游戏策划大赛],
  ),
)

#set text(
  lang: "zh",
  font: (fonts.sans, fonts.cjk-sans),
)

#set par(justify: true)

#show link: underline

#show raw: set text(
  font: (fonts.mono, fonts.sans, fonts.cjk-sans),
  size: 1.1em,
  slashed-zero: true,
  ligatures: false,
  features: (ss02: 1, ss06: 1),
)

#show raw.where(block: true): set text(
  size: 0.8em,
)

#show math.equation: set text(font: (fonts.math, fonts.serif, fonts.cjk-serif))

#title-slide()

= Outline <touying:hidden>

#components.adaptive-columns(outline(title: none, indent: 1em, depth: 1))

= 团队介绍

== 团队概况

上海交通大学 数学科学学院 2025 级本科生．

- *组成*：同一宿舍的三名数学系新生．
- *分工*：
  - *程序/策划/音乐*：负责全部代码工作，含核心架构、弹幕算法设计等．音乐制作和关卡设计．
  - *测试*：负责数值平衡、试玩测试和反馈．一部分系统机制的策划．
  - *美术*：负责部分美术资源，部分关卡设计．

= 设计思路

== 核心概念：重新定义「3D」与「共舞」

#grid(
  columns: (1fr, 1.5fr),
  gutter: 2em,
  [
    #set align(left)
    *「世界」即「舞台」*

    敌人不是要被消灭的靶子，而是配合演出的舞伴．

    *「时间」即「生命」*

    取消传统 HP / 残机．
    生命值 = 拍摄时长（镜时）．
    受击 = 自动回溯（NG 重拍）．
  ],
  [
    #box(fill: luma(240), inset: 1em, radius: 10pt)[
      *4D 体验公式*

      $ "3D Space" (X, Y, Z) + "Time" (T) $
      $ = "Tactical Dance" $
    ]

    *差异化体验*：
    - *传统 STG*：背板、底力、躲避．
    - *本作*：利用 Z 轴跳跃规避、利用时间回溯修正失误、利用擦弹积攒爆发．
  ],
)

= 开发里程碑

== 开发时间轴

#align(center)[
  #set text(size: 0.6em)
  #fletcher.diagram(
    node-stroke: 1pt,
    node-fill: luma(240),
    spacing: (10mm, 5mm),

    node((0, 0), [10.20\ 项目启动], shape: "rect", fill: blue.lighten(80%)),
    node((0, 1), [基础框架\ (Enemy, 3D Env)], shape: "rect"),
    edge((0, 0), (0, 1), "->"),

    node((1, 0), [10.25\ 核心机制], shape: "rect", fill: blue.lighten(80%)),
    node((1, 1), [Time Rewind\ Pause Menu], shape: "rect"),
    edge((1, 0), (1, 1), "->"),
    edge((0, 0), (1, 0), "->"),

    node((2, 0), [10.31\ 初赛冲刺], shape: "rect", fill: blue.lighten(80%)),
    node((2, 1), [Boss Combat\ Event System], shape: "rect"),
    edge((2, 0), (2, 1), "->"),
    edge((1, 0), (2, 0), "->"),

    node((4, 0), [12.25\ 复赛迭代 (Hyper)], shape: "rect", fill: orange.lighten(80%)),
    node((4, 1), [Hyper System\ Z-Axis Physics], shape: "rect"),
    edge((4, 0), (4, 1), "->"),
    edge((2, 0), (4, 0), "-->", label: "Feedback"),

    node((5, 0), [01.10\ 内容量产], shape: "rect", fill: green.lighten(80%)),
    node((5, 1), [Weapon Sys\ PhaseStellar\ Save/Load], shape: "rect"),
    edge((5, 0), (5, 1), "->"),
    edge((4, 0), (5, 0), "->"),
  )
]

= 迭代思路与核心优化

== 痛点 1：这也算 3D 游戏？

*初赛反馈*：虽有 3D 画面，但玩法逻辑仍停留在 2D 平面，Z 轴（高度）利用率极低．

*复赛解法：真正的立体弹幕*

- *重构弹幕系统*：引入 `PhaseRain`, `PhaseDrop`, `PhaseMaze` 等 7 个全新 Boss 阶段．
  - *PhaseSnow (大雪花)*：起教程作用的阶段，Boss 首先发射自机狙引导玩家擦满 Hyper，然后发射平面上的大雪花弹幕，让玩家学会使用 Hyper 的跳跃系统跳过弹幕实现躲避．
  - *PhaseRain (流星雨)*：Boss 飞至高空，计算提前量洒下弹幕雨配合自机狙．玩家必须在地面寻找缝隙，或利用 Hyper 机制跳跃躲避．
  - *PhaseDrop (泰山压顶)*：Boss 锁定玩家位置进行高空下坠攻击，并在落地时产生扩散的冲击波．玩家需要预判冲击波方向并跳跃躲避冲击波．
  - *PhaseWall (墙来了)*：两侧不断生成移动墙面强制玩家跟随移动．
  - *PhaseTree (魔豆开花)*：生成子弹构成的 3D 魔豆，花会变成自机狙．
- *视觉辅助*：落地指示器，将 Z 轴威胁可视化投影到地面，保证可玩性．
- *主动交互*：引入 *Z 轴跳跃* 动作，允许玩家物理上跳过低空弹幕．

== 痛点 2：无限回血的死循环

*初赛反馈*：后期 Build 成型后，擦弹回血效率过高，导致玩家可以无限「苟命」，缺乏紧张感．

*复赛解法：Hyper 系统 (资源转化)*

- *机制调整*：擦弹可以充能 Hyper Gauge．
- *Hyper 状态*：
  - 激活瞬间短时间无敌（摆脱困境）．
  - 赋予极高的垂直机动性（跳跃）．
  - 只有在 Hyper 期间擦弹才能延长爆发时间．
- *效果*：将「被动生存」转化为「资源管理 → 爆发输出」的进攻性循环．

  == 痛点 3：重复游玩体验

*复赛解法：系统深度扩展*

- *武器系统*：
  - *Violin*：高频低伤，适合积攒连击．（类比冲锋枪）
  - *Guitar*：蓄力散射，带有独特的 Aiming Cone 机制，改变操作节奏．（类比大狙）
- *存档系统*：
  - 实现了基于 JSON 的序列化 (`SaveManager`)，支持 Roguelite 长流程的中断与恢复．
- *更多 Boss 阶段*．

= 复盘与反思

== 做得好的地方

1. *架构设计*：
  - `RewindManager` 采用快照机制（Snapshot），实现了整个游戏世界（包括 Boss 阶段状态机、随机数种子）的完美回溯，技术难度高但效果极佳．
  - 每一帧的 `CaptureState` 和 `RestoreState` 保证了确定性．
2. *数学应用*：
  - 利用数学知识构建了众多具有数学美感的弹幕．
3. *完成度*：
  - 实现了完整的 Roguelite 循环（战斗 - 商店 - 事件 - Boss），内容量充实．

== 遇到的挑战与遗憾

1. *性能优化*：
  - *挑战*：3D 弹幕数量过多（3000+）导致卡顿．
  - *解决*：将 `Bullet` 类从繁重的对象继承重构为轻量级委托 (`Func<float, UpdateState>`)，大幅减少物理开销．
2. *设计遗憾*：
  - 时间回溯在叙事上的结合还可以更紧密．
  - Z 轴玩法的上手门槛较高，需要更好的引导．
  - 开发时间紧张，没有时间做好数值调整平衡，参数调试困难．
3. *美术问题*：
  - 缺乏美术设计．美术功底不强，没法展示精美的画面．

---

= 结语

== 结语

#[
  #set align(center)
  *感谢聆听*

  *模拟宇宙：时空舞台*

  #text(size: 0.8em)[*Simulated Universe: Chrono Stage*]

  #v(1em)
  #text(size: 0.8em)[上海交通大学 Untitled]
]
