import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputDir = "E:/AXR_Projects/unity/Anchor/outputs/player_attribute_core";
const workbook = Workbook.create();
const sheet = workbook.worksheets.add("核心属性");

sheet.showGridLines = true;

sheet.getRange("A1:C1").values = [["属性", "画面表现", "氛围表现"]];
sheet.getRange("A2:C9").values = [
  ["周行动力", "日历格、便签、体力点、咖啡杯数量。每用一次就划掉一个格子或喝空一杯咖啡。", "一周被压缩成几个选择，像在办公室里抢时间。"],
  ["Bug值", "屏幕裂纹、红色报错弹窗、代码乱码、程序区冒烟。数值越高，画面越吵、越红。", "项目正在失控，玩家不是在开发，而是在灭火。"],
  ["士气", "员工表情、工位状态、聊天气泡、办公室灯光。高士气时明亮热闹，低士气时灰暗沉默。", "团队还撑不撑得住，是办公室最直观的温度。"],
  ["金币", "工资信封、预算账本、钱包、硬币堆。花钱时可以表现为玩家把工资塞进项目里。", "钱不是奖励，而是最后的安全垫。"],
  ["愿望单", "网页后台数字、弹幕、点赞、玩家评论、排行榜。月结算时数字跳动增长。", "外面的玩家正在看着你们，期待感和压力一起上涨。"],
  ["质量分", "不直接显示为数字，用试玩反馈、媒体短评、弹幕语气、评分星级暗示。", "这游戏到底有没有灵魂，玩家只能从反馈里感觉出来。"],
  ["美术完成度", "美术区画板、素材堆、角色立绘、UI草图逐渐变完整。低时是线稿，高时变成成品。", "游戏开始有样子了，办公室里能看到作品慢慢成型。"],
  ["音效完成度", "音效区音轨、波形、喇叭、节拍器。完成度越高，界面反馈和办公室环境声越丰富。", "游戏从无声样机变得有反馈、有节奏、有情绪。"],
];

sheet.getRange("A1:C1").format = {
  font: { bold: true },
  fill: "#E5E7EB",
};
sheet.getRange("A:C").format.wrapText = true;
sheet.getRange("A:A").format.columnWidthPx = 120;
sheet.getRange("B:B").format.columnWidthPx = 420;
sheet.getRange("C:C").format.columnWidthPx = 360;
sheet.getRange("A1:C9").format.borders = { preset: "all", style: "thin", color: "#D1D5DB" };
sheet.freezePanes.freezeRows(1);

const inspect = await workbook.inspect({
  kind: "table",
  range: "核心属性!A1:C9",
  include: "values",
  tableMaxRows: 9,
  tableMaxCols: 3,
  maxChars: 2500,
});
console.log(inspect.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
});
console.log(errors.ndjson);

await fs.mkdir(outputDir, { recursive: true });
const preview = await workbook.render({ sheetName: "核心属性", autoCrop: "all", scale: 1, format: "png" });
await fs.writeFile(`${outputDir}/核心属性_简版.png`, new Uint8Array(await preview.arrayBuffer()));

const xlsx = await SpreadsheetFile.exportXlsx(workbook);
await xlsx.save(`${outputDir}/玩家核心属性系统表.xlsx`);
console.log(`${outputDir}/玩家核心属性系统表.xlsx`);
