#let fonts = (
  mono: "IBM Plex Mono",
  serif: "IBM Plex Serif",
  sans: "IBM Plex Sans",
  math: "IBM Plex Math",
  cjk-serif: "Source Han Serif",
  cjk-sans: "Sarasa UI SC",
)

#show math.equation: set text(font: (fonts.math, fonts.serif, fonts.cjk-serif))

#show raw: set text(
  font: (fonts.mono, fonts.sans, fonts.cjk-sans),
  slashed-zero: true,
  ligatures: false,
  features: (ss02: 1, ss06: 1),
)

#set text(
  lang: "zh",
  font: (fonts.serif, fonts.cjk-serif),
)

#set page(
  paper: "a4",
  margin: (x: 2.5cm, y: 2.5cm),
)

#align(center)[
  #block(
    width: 100%,
    inset: 10pt,
    fill: luma(240),
    radius: 4pt,
  )[
    #text(size: 24pt, weight: "bold")[模拟宇宙：时空舞台]
    #v(1em)
    #text(size: 16pt)[复赛迭代设计与开发报告]
  ]
]

#pagebreak()

#outline(
  title: [目录],
  depth: 2,
)

#pagebreak()

= 引言：从「躲避」到「共舞」的进化

在初赛阶段，我们构建了《模拟宇宙：时空舞台》的核心框架：一个以「时间」为生命、以「回溯」为容错机制的 Roguelite 弹幕射击游戏．

经过初赛的测试与反馈，我们意识到初版设计存在两个核心痛点：

1. *「4D」概念的维数塌缩*：虽然使用了 3D 引擎，但游戏玩法主要局限在 XY 平面，Z 轴（高度）的利用率极低，导致「立体弹幕」名不副实．
2. *资源循环的后期疲软*：初期的「擦弹（Graze）= 回复时间（生命）」机制在后期 Build 成型后，导致玩家生存压力骤减，擦弹收益边际递减严重，失去了「在刀尖起舞」的紧张感．

复赛阶段，我们对底层机制进行了重构，引入了 *Hyper 系统*、*多武器系统*，并全面重制了 *Boss 阶段*，真正实现了对 Z 轴高度的战术利用．

= 核心机制迭代：Hyper 系统

为了解决擦弹收益递减的问题，并赋予玩家更强的主动权，我们引入了 *Hyper 爆发机制*．

== 机制逻辑

- *Hyper 爆发槽*：显示在生命值下方．玩家不再仅仅为了「回血」而擦弹，擦弹现在的核心收益是积攒 Hyper 槽．
- *激活方式*：当 Hyper 槽满时，按下 Hyper 键#strong[（默认键位为空格）]激活．
- *Hyper 状态效果*：
  1. *高度轴机动*：Hyper 激活时，玩家获得极强的垂直机动能力，可以跳跃至高空，物理上规避地面判定的所有弹幕．
  2. *擦弹续航*：在 Hyper 期间擦弹不再仅仅回复生命，而是延长 Hyper 状态的持续时间．

== 设计意图

这一改动将游戏的博弈循环从「受击 -> 回溯 -> 苟活」转变为「擦弹积攒 -> 开启 Hyper -> 实现更灵活的走位」．它鼓励玩家在非 Hyper 期间积极擦弹，在危险时刻通过 Hyper 进行维度打击（跳到 Z 轴上方），完美契合了「与世界共舞」的主题——不仅是在平面上躲避，更是在立体空间中通过爆发来掌控节奏．

= 维度突破：真正的 3D 弹幕设计

为了回应「Z 轴利用率低」的反馈，我们重写了弹幕与 Boss 行为逻辑，确保高度轴成为玩法的核心变量．

== 典型的新增 Boss 阶段

我们新增了多个强制玩家进行 Z 轴交互的 Boss 阶段：

- *PhaseSnow (大雪花)*：起教程作用的阶段，Boss 首先发射自机狙引导玩家擦满 Hyper，然后发射平面上的大雪花弹幕，让玩家学会使用 Hyper 的跳跃系统跳过弹幕实现躲避．
- *PhaseRain (流星雨)*：Boss 飞至高空，计算提前量洒下弹幕雨配合自机狙．玩家必须在地面寻找缝隙，或利用 Hyper 机制跳跃躲避．
- *PhaseDrop (泰山压顶)*：Boss 锁定玩家位置进行高空下坠攻击，并在落地时产生扩散的冲击波．玩家需要预判冲击波方向并跳跃躲避冲击波．
- *PhaseWall (墙来了)*：两侧不断生成移动墙面强制玩家跟随移动．
- *PhaseTree (魔豆开花)*：生成子弹构成的 3D 魔豆，花会变成自机狙．
- 等等共 7 个新增阶段，全部围绕「充分利用 Z 轴」展开．

= 系统扩充

== 武器系统

我们将原本单一的射击模式扩展为可更换的武器系统，以适应不同玩家的风格：

- *Violin*：高射速、低伤害，适合持续输出和触发攻击特效．
- *Guitar*：低射速、高爆发，拥有瞄准锥形范围机制，按住慢速键可聚焦准星，松开后进行散射爆发．

== 存档与持久化

引入了基于 JSON 的序列化系统，支持保存当前 Run 的所有状态（种子、强化、血量、事件进度）．这使得 Roguelite 的长流程体验更加友好．

== 性能优化

重构了弹幕池（Bullet Pool），将 `SimpleBullet` 的逻辑从复杂的对象继承改为基于 `Func<float, UpdateState>` 的委托模式．这使得我们能够同屏渲染数千颗具有复杂 3D 轨迹（如贝塞尔曲线生成的树状弹幕）的子弹，而依然保持 60+ FPS 的流畅度．

= 结语

复赛版本的《模拟宇宙：时空舞台》通过 Hyper 系统与深度 Z 轴关卡的结合，我们构建了一个真正的立体时空舞台．每一次擦弹、每一次起跳、每一次时间回溯，都是玩家在这个四维舞台上谱写的独特乐章．
