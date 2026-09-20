# M0 像素宠物素材选择

核对日期：2026-09-20。这里记录已选占位方案和候选比较，不把素材下载进仓库；M1 实际导入素材时还需保存来源页和许可证页面快照。

PRD 第 6.3 节需要：待机、悬停、拖动、庆祝、提醒、思考，共 6 种逐帧像素动画。

## 已选方案

用户于 2026-09-20 确认选择 **Desktop Cat 的 8 状态橙猫 PNG** 作为 v1 开发期占位素材，未来可补画或替换。M1 暂定映射：

| Roost 状态 | 占位映射 |
|---|---|
| 待机 | `idle` |
| 悬停 | `idle` + 素材已有的 tail-wag 悬停效果 |
| 拖动 | `waking`，作为“被拎起”的临时视觉反馈 |
| 庆祝 | `happy` |
| 提醒 | `notification` |
| 思考 | `thinking` |

这套映射保证六种产品状态都有统一形象反馈，但悬停和拖动不是最终逐帧动画。M1 完成后再补画或替换；v1 发布前按 PRD 6.5 补齐 6/6，并把 MIT 版权与许可文本同时放入“关于”页和仓库。素材导入时保存许可证页面快照。

| 候选 | 来源 | 许可证 | 可否随应用再分发 | 是否需要署名 | 对 6 种动画的覆盖 |
|---|---|---|---|---|---|
| Desktop Cat 的 8 状态橙猫 PNG | [GitHub 仓库](https://github.com/llhuanhuan/desktop-cat) / [MIT LICENSE](https://github.com/llhuanhuan/desktop-cat/blob/main/LICENSE) | MIT，仓库根许可证；README 明确素材位于 `renderer/assets/cat/processed/` | 可以修改、发布、再分发，但必须保留版权与许可声明 | **需要**：`Copyright (c) 2025 Administrator` 和 MIT 文本 | 原生覆盖：待机（idle）、庆祝（happy）、提醒（notification）、思考（thinking）；悬停由 tail-wag 效果覆盖但不是独立 PNG；拖动只有交互、没有专用“被拎起”帧。**4/6 原生，1/6 效果可借用，1/6 缺失。** |
| 16 px Free Pixel Animation: Cat [6 loops] | [itch.io 来源页](https://zeenaz.itch.io/free-pixel-animation-cat-6-loops) | CC0 1.0 / Public Domain（来源页明确写明） | 可以，允许修改和商业再分发 | 不需要（可自愿署名 Zeenaz） | stand 可作待机；set/sit 可作悬停；walk 可勉强改作拖动；scared/frighten 可作提醒。没有合适的庆祝和思考。**4/6 需语义改配，2/6 缺失。** |
| Dog（6 行动画 sprite sheet） | [OpenGameArt 来源页](https://opengameart.org/content/dog-3) | CC0 | 可以，允许修改和商业再分发 | 不需要（可自愿署名 rmazanek，并保留其列出的上游来源更稳妥） | Idle Stand / Idle Sit 覆盖待机；Sit Transition 可作悬停；Walk / Run 可改作拖动；Bark 可作提醒。没有庆祝和思考。**4/6 可用，2/6 缺失。** |

## 候选比较结论

三个候选里，**Desktop Cat 最接近 PRD 语义**，但仍缺专用拖动帧，而且悬停不是独立逐帧 PNG。两个 CC0 候选授权最省事，却都缺庆祝和思考。用户已接受先使用 Desktop Cat 占位、未来补画或替换的取舍。

不建议把 [immaotianyi/pixel-cat](https://github.com/immaotianyi/pixel-cat) 作为 v1 素材：它虽有 idle / curious / walking / alert / sleeping / happy 六种语义状态并采用 MIT，但主体是 SVG + CSS 连续动画，不符合 PRD 第 6.3、6.4 节的逐帧像素动画要求。
