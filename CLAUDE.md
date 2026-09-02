<!-- 维护者备注：本文件会注入每个 session 的上下文，目标控制在 200 行以内。
     HTML 注释会在注入前被剥离，可以放心在这里写给人看的说明。
     体积检查：定期运行 /doctor，它会建议裁掉 Claude 能从代码库自行推断的内容。 -->

## 协作前提

- 涉及产品取舍时，要有后果描述而不仅仅是技术描述（说"安装包会大 80MB"，除了说"会引入 Chromium"）。
- 不确定的地方停下来问，不要替所有者做假设。

## 安全红线

## Git 规范

- `main` 是保护分支，禁止直接提交。所有改动走 `feat/xxx`、`fix/xxx`、`docs/xxx`、`chore/xxx` 分支。
- 禁止 `git push --force`（含 `--force-with-lease`）到任何共享分支。
- Commit message 用英文，遵循 Conventional Commits：`feat: `、`fix: `、`docs: `、`refactor: `、`test: `、`perf: `、`chore: `、`build: `、`ci: `。
- 一个 commit 一件事。完成一个可独立验证的单元就提交，不要攒成大 commit。
- 提交前必须本地跑通 lint 和测试（测试构建完成后）。测试失败不提交，不要用 `--no-verify` 绕过 hook。
- 禁止提交：任何密钥或 `.env`、构建产物、超过 1MB 的二进制文件（美术素材需单独批准）、`CLAUDE.local.md`。
- push、PR、打tag等其他工作都由用户手动来做，你只需要提建议


## 依赖引入规范

## 文档与决策

- 中文写文档，英文写代码注释和 commit message。

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