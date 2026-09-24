using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    // PRD 9.6：用虚构待办的测试题检验模型，结果只提示，不限制使用。
    internal sealed class AiSelfTestForm : Form
    {
        private readonly AiEndpoint endpoint;
        private readonly AiEvalSuite suite;
        private readonly List<AiEvalCase> cases;
        private readonly ProgressBar progress;
        private readonly Label status;
        private readonly Label summary;
        private readonly TextBox details;
        private readonly Button start;
        private CancellationTokenSource running;

        internal AiSelfTestForm(AiEndpoint endpoint, string suitePath)
        {
            this.endpoint = endpoint;
            suite = AiEvalSuite.Load(suitePath);
            cases = suite.SelfTestCases();
            Text = "模型测试题";
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 500);

            Label intro = new Label
            {
                Text = string.Format("用 {0} 道测试题检验「{1}」改计划靠不靠谱。\r\n题目只使用虚构的待办，不会发送你自己的待办。会产生少量模型费用，大约需要 1 分钟。", cases.Count, endpoint.Model),
                Location = new Point(18, 16),
                Size = new Size(524, 44)
            };
            progress = new ProgressBar { Location = new Point(18, 70), Size = new Size(524, 20), Maximum = cases.Count };
            status = new Label { Location = new Point(18, 96), Size = new Size(524, 20), ForeColor = Color.DimGray };
            summary = new Label { Location = new Point(18, 122), Size = new Size(524, 64), Font = new Font(Font, FontStyle.Bold) };
            details = new TextBox
            {
                Location = new Point(18, 190),
                Size = new Size(524, 250),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window
            };
            start = new Button { Text = "开始测试", Location = new Point(356, 452), Size = new Size(90, 34) };
            start.Click += delegate
            {
                if (running != null) running.Cancel();
                else Start();
            };
            Button close = new Button { Text = "关闭", Location = new Point(452, 452), Size = new Size(90, 34), DialogResult = DialogResult.Cancel };
            CancelButton = close;
            Controls.AddRange(new Control[] { intro, progress, status, summary, details, start, close });
            FormClosed += delegate { if (running != null) running.Cancel(); };
        }

        internal int QuestionCountForTest { get { return cases.Count; } }
        internal bool RunningForTest { get { return running != null; } }
        internal string SummaryForTest { get { return summary.Text; } }
        internal string DetailsForTest { get { return details.Text; } }

        internal void StartForTest()
        {
            Start();
        }

        private async void Start()
        {
            running = new CancellationTokenSource();
            start.Text = "取消";
            progress.Value = 0;
            summary.Text = string.Empty;
            details.Text = string.Empty;
            status.Text = string.Format("正在答题：0 / {0}", cases.Count);
            AiClient client = new AiClient();
            List<AiEvalResult> results = null;
            string failure = null;
            try
            {
                results = await suite.RunAsync(cases, delegate(AiRequestContext context, string input, CancellationToken token)
                {
                    return client.CompleteAsync(endpoint, context.SystemPrompt(), input, 2048, token);
                }, delegate(int done)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed) return;
                        progress.Value = done;
                        status.Text = string.Format("正在答题：{0} / {1}", done, cases.Count);
                    });
                }, running.Token);
            }
            catch (OperationCanceledException)
            {
                failure = "测试已取消。";
            }
            catch (AiException exception)
            {
                failure = exception.Kind == AiFailureKind.Cancelled ? "测试已取消。" : "测试中断：" + exception.Message;
            }
            finally
            {
                running.Dispose();
                running = null;
            }
            if (IsDisposed) return;
            start.Text = "重新测试";
            if (failure != null)
            {
                status.Text = failure;
                return;
            }
            status.Text = "测试完成。";
            ShowResults(results);
        }

        private void ShowResults(List<AiEvalResult> results)
        {
            int passed = 0;
            Dictionary<string, int> critical = new Dictionary<string, int>();
            StringBuilder text = new StringBuilder();
            foreach (AiEvalResult result in results)
            {
                if (result.Pass) { passed++; continue; }
                foreach (string label in result.Critical)
                    critical[label] = (critical.ContainsKey(label) ? critical[label] : 0) + 1;
                text.Append(result.Critical.Count > 0 ? "⚠ " : "✗ ").Append("你说：").AppendLine(result.Case.Input);
                text.Append("    应当：").AppendLine(suite.DescribeExpectation(result.Case));
                text.Append("    模型：").AppendLine(DescribeActual(result.Plan));
                text.Append("    问题：").AppendLine(result.Reason);
                text.AppendLine();
            }

            if (critical.Count == 0)
            {
                summary.ForeColor = passed == results.Count ? Color.SeaGreen : Color.FromArgb(60, 60, 60);
                summary.Text = string.Format("答对 {0} / {1} 题，没有出现会误改计划的严重错误。", passed, results.Count);
            }
            else
            {
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, int> entry in critical) parts.Add(string.Format("{0}（{1} 题）", entry.Key, entry.Value));
                summary.ForeColor = Color.Firebrick;
                summary.Text = string.Format("答对 {0} / {1} 题。⚠ 出现严重错误：{2}。\r\n仍然可以使用——每次改动都要你确认——但请仔细核对预览，或考虑换一个模型。",
                    passed, results.Count, string.Join("、", parts.ToArray()));
            }
            details.Text = text.Length == 0 ? "全部答对。" : text.ToString().TrimEnd();
        }

        private static string DescribeActual(AiPlan plan)
        {
            if (plan.Status == AiPlanStatus.Unclear) return "说没听懂（" + plan.Message + "）";
            if (plan.Status == AiPlanStatus.Ambiguous) return "说有歧义，没有改动";
            List<string> parts = new List<string>();
            foreach (AiOperation operation in plan.Operations)
                parts.Add((operation.IsValid ? string.Empty : "[无效] ") + operation.Summary);
            return parts.Count == 0 ? "没有改动" : string.Join("；", parts.ToArray());
        }

        internal static string SuitePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "ai-eval", "cases.json"); }
        }
    }
}
