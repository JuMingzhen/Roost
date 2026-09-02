<!-- 维护者备注：本文件会注入每个 session 的上下文，目标控制在 200 行以内。
     HTML 注释会在注入前被剥离，可以放心在这里写给人看的说明。
     体积检查：定期运行 /doctor，它会建议裁掉 Claude 能从代码库自行推断的内容。 -->

## 项目是什么

Roost：Windows 桌面上的像素桌宠，帮用户整理计划（to-do）。宠物旁常驻一个可折叠的迷你清单，用户可以用一句话（文字或语音）让 AI 改计划。

**当前阶段：M0 之前，仓库里还没有产品代码。** 详见 `docs/MILESTONES.md`。

## 协作前提

- 涉及产品取舍时，要有后果描述而不仅仅是技术描述（说"安装包会大 80MB"，除了说"会引入 Chromium"）。
- 不确定的地方停下来问，不要替所有者做假设。

## 开工前必读

**任何非琐碎的改动，动手前先读 `docs/DECISIONS.md`。** 那里记录了已经权衡过的十条决策和它们的代价。如果你的方案会推翻其中一条，先看该条的「复议条件」，然后提案，不要直接改。

`docs/PRD.md` 里有一份 **v1 明确不做的清单**（重复任务、用户上传形象、账号同步、开机自启、mac 版等）。不要"顺手"实现里面的任何一项。

## 项目地图

| 你要做什么 | 先读 | 代码在 |
| --- | --- | --- |
| 改产品行为、加功能 | `docs/PRD.md` + `docs/DECISIONS.md` | — |
| 改窗口、点击穿透、托盘 | `docs/ARCHITECTURE.md` | `src-tauri/src/window/` |
| 改数据库、字段、迁移 | `docs/DATA-MODEL.md` | `src-tauri/src/db/` |
| 改 AI 行为、工具、prompt | `docs/AI-PIPELINE.md` | `src-tauri/src/ai/` |
| 改提醒逻辑 | `docs/PRD.md` 提醒一节 | `src-tauri/src/scheduler/` |
| 改宠物动画、sprite | `docs/PET-ASSETS.md` | `src/pet/`、`src-tauri/assets/pets/` |
| 改清单 UI、今日视图 | `docs/PRD.md` 形态定义 | `src/todo/` |
| 改设置页 | `docs/AI-PIPELINE.md` 能力探测一节 | `src/settings/` |
| 排期、里程碑验收标准 | `docs/MILESTONES.md` | — |

目录结构的完整说明在 `docs/ARCHITECTURE.md`。M1 之前这些目录还不存在。

## 技术栈与常用命令

Tauri v2（Rust 后端 + TypeScript 前端），本地 SQLite。前端框架在 M1 开工时确定。

<!-- 下面的命令是规划，M1 脚手架落地后必须回来校正成真实可用的命令。 -->

```
pnpm tauri dev          # 本地运行
cargo clippy            # Rust lint
cargo test              # Rust 测试
pnpm lint               # 前端 lint
```

## 安全红线

- **API Key 只存系统凭据管理器**（Windows 凭据管理器 / mac Keychain）。不写配置文件、不写数据库、不落明文、不打日志。
- **前端不得持有 API Key，不得直接向外部服务发请求。** 所有外部调用走 Rust 侧。前端资源容易被提取，key 落到前端等于泄露。
- **不得把用户的待办内容写进日志或错误上报。** 待办是私人内容，报错时只记类型和 id，不记 title 和 note。
- **AI 产生的写操作一律不直接落库**，必须经过变更预览和用户确认（`docs/AI-PIPELINE.md`）。删除永远需要确认，没有例外。
- 数据库删除走软删除，不物理删除。

## Git 规范

- `main` 是保护分支，禁止直接提交。所有改动走 `feat/xxx`、`fix/xxx`、`docs/xxx`、`chore/xxx` 分支。
- 禁止 `git push --force`（含 `--force-with-lease`）到任何共享分支。
- Commit message 用英文，遵循 Conventional Commits：`feat: `、`fix: `、`docs: `、`refactor: `、`test: `、`perf: `、`chore: `、`build: `、`ci: `。
- 一个 commit 一件事。完成一个可独立验证的单元就提交，不要攒成大 commit。
- 提交前必须本地跑通 lint 和测试（测试构建完成后）。测试失败不提交，不要用 `--no-verify` 绕过 hook。
- 禁止提交：任何密钥或 `.env`、构建产物、超过 1MB 的二进制文件（美术素材需单独批准）、`CLAUDE.local.md`。
- push、PR、打tag等其他工作都由用户手动来做，你只需要提建议

## 依赖引入规范

- **新增依赖需要提案**，说明：解决什么问题、对安装包体积的影响、维护状态（最近更新时间、issue 活跃度）、有没有更轻的替代。桌宠是常驻程序，体积和内存是产品卖点（见 D1），不是技术细节。
- Rust 侧优先用标准库或已有依赖，不为一个小功能引入大 crate。
- **前端不引入 UI 组件库。** 桌宠界面高度定制，组件库带来的样式九成用不上，只会增重。
- 不引入需要额外运行时（Python、JVM 等）的依赖。

## 文档与决策

- 中文写文档，英文写代码注释和 commit message。
- **改动了行为就同步改文档。** 尤其是：AI 工具契约变了要改 `docs/AI-PIPELINE.md`，表结构变了要改 `docs/DATA-MODEL.md`，sprite 规格变了要改 `docs/PET-ASSETS.md`。
- 做出新的产品或架构决策时，追加到 `docs/DECISIONS.md`，格式照已有条目：决策、备选、理由、**后果（含负面）**、复议条件。

## 本文件的更新规范

`CLAUDE.md` 由所有者掌管，**Agent 不得自行修改**，除非用户主动提出修改或者批准你的修改提议。

当你认为有必要修改时，向所有者提出建议。
所有者回复"批准"后再修改文件，并在同一 commit 中只做这一件事（`docs: update CLAUDE.md — <一句话>`）。

裁剪同样需要提案。本文件应保持在 200 行以内；接近上限时，优先把只对局部代码有效的规则迁移到 `.claude/rules/`（配 `paths:` 前缀限定），把多步流程迁移到 skill。

## 扩展机制的创建规范

如果认为有必要新增 skill、subagent、hook、`.claude/rules/` 文件，**及时提案、获批后再创建**。禁止未经批准就写入 `.claude/` 目录。

选择依据（照此判断，不要滥用）：

| 需求性质 | 用什么 |
| --- | --- |
| 必须被强制执行，不能靠自觉 | **hook**（或 settings 里的 permissions） |
| 只在改动某类文件时才需要的规则 | **`.claude/rules/` + `paths:` 前缀** |
| 按需加载的多步流程或方法论 | **skill** |
| 需要独立上下文的大体量子任务 | **subagent** |
| 每个 session 都要知道的短规则 | **CLAUDE.md** |
