# 数据模型

> 前置阅读：[DECISIONS.md](./DECISIONS.md) 的 D4（纯本地 SQLite，无账号）。

存储为单个本地 SQLite 文件，位于系统的应用数据目录下。

---

## `tasks`

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | TEXT PK | UUID。**不用自增整数**——将来接同步时自增 ID 必然冲突，现在用 UUID 零成本 |
| `title` | TEXT NOT NULL | 待办标题 |
| `note` | TEXT | 备注，可空 |
| `due_at` | INTEGER | 截止时间，Unix 毫秒时间戳，UTC。可空（无期限待办） |
| `all_day` | INTEGER | 0/1。「明天开会」有具体时刻，「明天交报告」可能只有日期，展示和提醒逻辑不同 |
| `progress` | INTEGER NOT NULL | 0–100（D9：进度不是二元勾选） |
| `status` | TEXT NOT NULL | `active` / `done` / `archived` |
| `created_at` | INTEGER NOT NULL | Unix 毫秒 |
| `updated_at` | INTEGER NOT NULL | Unix 毫秒，任何写操作都要更新 |
| `deleted_at` | INTEGER | 软删除时间，可空 |

**`progress` 与 `status` 的关系**：`progress = 100` 不自动等于 `done`，用户可能只是标记做完了大部分。由用户显式完成，或在 UI 上把「拖到 100」和「点完成」做成同一个动作——具体交互在 M2 定，但数据层保持两者独立。

### 为什么保留 `updated_at` 和软删除

D4 决定 v1 不做同步，但这两个字段现在加进来成本不到一天，将来接同步时不用做数据迁移，也不用逼用户重建所有待办。软删除还顺带让「撤销删除」变得几乎免费——AI 误删是本产品的真实风险（见 `AI-PIPELINE.md`）。

### 索引

- `due_at`（今日视图和提醒扫描的主查询路径）
- `status`

---

## `reminders`

到点提醒的状态。拆表而不是塞进 `tasks`，是因为一条待办可能有多次提醒（原定时间 + 稍后提醒）。

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | TEXT PK | UUID |
| `task_id` | TEXT NOT NULL | 外键 → `tasks.id` |
| `fire_at` | INTEGER NOT NULL | 触发时间，Unix 毫秒 |
| `fired_at` | INTEGER | 实际触发时间，可空。用于避免重复提醒 |
| `dismissed` | INTEGER NOT NULL | 0/1 |

---

## `settings`

简单的 key-value 表，存外观、提醒偏好、`base_url`、`model` 等。

**不存 API Key**——key 走系统凭据管理器（见 `ARCHITECTURE.md`）。

---

## 时间处理约定

- **数据库一律存 UTC 毫秒时间戳**，展示时才转本地时区。
- 「今天」的边界由用户设定的一天起点决定（默认 04:00 而不是 00:00——凌晨两点还在干活的人，心理上仍属于前一天）。这个规则同时被今日视图和 AI 的相对时间解析使用，必须只有一处实现，见 `AI-PIPELINE.md`。

---

## 迁移

- 迁移脚本按序号命名，只增不改。已发布的迁移**永远不修改**，要改就加新的一条。
- 每个迁移必须能在空库和已有数据的库上都正确执行。
- 应用启动时检查版本并按序执行。

---

## 导出

D4 的强制配套。导出为单个 JSON 文件，包含全部未删除的 `tasks` 和 `reminders`，附带导出时间和 schema 版本。

导出格式要能被自己重新导入——虽然 v1 不做导入 UI，但格式必须保证这件事将来可行，否则「导出」只是一个心理安慰。
