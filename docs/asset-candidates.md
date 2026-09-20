# M0 像素宠物素材候选

核对日期：2026-09-20。这里只记录候选，不把素材下载进仓库。最终采用前还需保存来源页和许可证页面快照。

PRD 第 6.3 节需要：待机、悬停、拖动、庆祝、提醒、思考，共 6 种逐帧像素动画。

| 候选 | 来源 | 许可证 | 可否随应用再分发 | 是否需要署名 | 对 6 种动画的覆盖 |
|---|---|---|---|---|---|
| Desktop Cat 的 8 状态橙猫 PNG | [GitHub 仓库](https://github.com/llhuanhuan/desktop-cat) / [MIT LICENSE](https://github.com/llhuanhuan/desktop-cat/blob/main/LICENSE) | MIT，仓库根许可证；README 明确素材位于 `renderer/assets/cat/processed/` | 可以修改、发布、再分发，但必须保留版权与许可声明 | **需要**：`Copyright (c) 2025 Administrator` 和 MIT 文本 | 原生覆盖：待机（idle）、庆祝（happy）、提醒（notification）、思考（thinking）；悬停由 tail-wag 效果覆盖但不是独立 PNG；拖动只有交互、没有专用“被拎起”帧。**4/6 原生，1/6 效果可借用，1/6 缺失。** |
| 16 px Free Pixel Animation: Cat [6 loops] | [itch.io 来源页](https://zeenaz.itch.io/free-pixel-animation-cat-6-loops) | CC0 1.0 / Public Domain（来源页明确写明） | 可以，允许修改和商业再分发 | 不需要（可自愿署名 Zeenaz） | stand 可作待机；set/sit 可作悬停；walk 可勉强改作拖动；scared/frighten 可作提醒。没有合适的庆祝和思考。**4/6 需语义改配，2/6 缺失。** |
| Dog（6 行动画 sprite sheet） | [OpenGameArt 来源页](https://opengameart.org/content/dog-3) | CC0 | 可以，允许修改和商业再分发 | 不需要（可自愿署名 rmazanek，并保留其列出的上游来源更稳妥） | Idle Stand / Idle Sit 覆盖待机；Sit Transition 可作悬停；Walk / Run 可改作拖动；Bark 可作提醒。没有庆祝和思考。**4/6 可用，2/6 缺失。** |

## 结论

三个候选里，**Desktop Cat 最接近 PRD 语义**，但仍缺专用拖动帧，而且悬停不是独立逐帧 PNG。两个 CC0 候选授权最省事，却都缺庆祝和思考。因此目前没有一个候选能原样满足 6/6；若选择任一候选，需同时决定：允许基于同一画风补画缺失动画，还是继续寻找完整素材包。

不建议把 [immaotianyi/pixel-cat](https://github.com/immaotianyi/pixel-cat) 作为 v1 素材：它虽有 idle / curious / walking / alert / sleeping / happy 六种语义状态并采用 MIT，但主体是 SVG + CSS 连续动画，不符合 PRD 第 6.3、6.4 节的逐帧像素动画要求。
