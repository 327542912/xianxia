package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.Clause;
import com.novelengine.core.Models.GateResult;
import com.novelengine.core.Models.Ledger;
import com.novelengine.core.Models.OutlineChapter;
import com.novelengine.core.Models.StylePack;
import java.util.List;

/** 提示词装配。所有文本在此单点定义，别处只引用，避免多处漂移。 */
public final class Prompts {

    private Prompts() {}

    public static final String PLANNER_SYSTEM =
        "你是资深网文策划编辑，同时懂结构工程。你的唯一任务是把作者给的三要素（题材、主角人设、核心冲突）扩成一份可执行的开篇方案。\n"
      + "硬性要求：\n"
      + "1. 只输出一个 JSON 对象，不要任何解释、不要 Markdown 代码块、不要前后缀文字。\n"
      + "2. 大纲必须正好 10 章，章号 1-10 连续。每章必须有 coreEvent（本章不可逆的核心事件）与 cliffhanger（章末悬念，必须具体到物证、动作或未答问题，禁止“欲知后事”式空话）。\n"
      + "3. 人物关系要能产生压力，不要和和气气。爽点要落在“读者当下能获得什么”，反转伏笔要写清埋在哪一章、第几章兑现。\n"
      + "4. 主角人设里的缺陷必须在大纲前 3 章造成一次真实损失，不允许设定与剧情两张皮。\n"
      + "5. 语言简洁，避免形容词堆砌；每章标题不超过 12 字。";

    public static String plannerUser(AppConfig c, StylePack pack) {
        StringBuilder sb = new StringBuilder();
        sb.append("帮我策划一部【").append(nz(c.genre)).append("】原创小说，主角人设为【").append(nz(c.hero))
          .append("】，核心冲突是【").append(nz(c.conflict)).append("】。")
          .append("结合目标读者喜好，设计人物关系、剧情爽点和反转伏笔，输出故事主线及前10章大纲，每章标注核心事件和结尾悬念。\n\n");
        sb.append("【补充条件】\n");
        sb.append("- 作品名：").append(nz(c.title)).append('\n');
        sb.append("- 目标平台与读者：").append(c.platform == null || c.platform.trim().isEmpty() ? "付费网文，男频，追更型读者" : c.platform.trim()).append('\n');
        sb.append("- 单章目标字数：").append(c.targetChars).append(" 字\n");
        if (c.world != null && !c.world.trim().isEmpty()) sb.append("- 世界与规则要点：").append(c.world.trim()).append('\n');
        if (c.resource != null && !c.resource.trim().isEmpty()) sb.append("- 世界最稀缺的资源：").append(c.resource.trim()).append('\n');
        if (pack != null) sb.append("- 文风定位：").append(nz(pack.positioning)).append('\n');
        sb.append('\n');
        sb.append("【输出 JSON 结构（键名不得改动，值用中文）】\n");
        sb.append("{\n");
        sb.append("  \"logline\": \"一句话故事（不超过 40 字）\",\n");
        sb.append("  \"mainline\": \"故事主线，按 起因→升级→转折→代价→悬念 写成一段（150-260 字）\",\n");
        sb.append("  \"factions\": [\"势力或阵营，3-5 个\"],\n");
        sb.append("  \"relations\": [{\"a\":\"角色A\",\"b\":\"角色B\",\"relation\":\"关系本质\",\"tension\":\"这段关系的压力来源\"}],\n");
        sb.append("  \"hooks\": [{\"name\":\"爽点名\",\"where\":\"第几章\",\"payoff\":\"读者当下获得什么\"}],\n");
        sb.append("  \"twists\": [{\"name\":\"反转/伏笔名\",\"where\":\"埋设章\",\"payoff\":\"兑现章与兑现方式\"}],\n");
        sb.append("  \"chapters\": [{\"no\":1,\"title\":\"章标题\",\"stage\":\"推进/冲突/揭示/收束\",\"coreEvent\":\"本章不可逆的核心事件\",\"cliffhanger\":\"章末悬念（具体）\",\"cast\":[\"出场角色\"],\"threads\":[\"本章新埋或推进的线索\"]}]\n");
        sb.append("}\n");
        sb.append("要求：relations 至少 5 条，hooks 至少 6 条，twists 至少 4 条，chapters 正好 10 条。");
        return sb.toString();
    }

    /** 自检失败后的一次修复请求：只带问题清单，不重写全案。 */
    public static String plannerRepair(String rawJson, List<String> problems) {
        StringBuilder sb = new StringBuilder();
        sb.append("下面这份策划 JSON 未通过校验，请只修正指出的问题后重新输出完整 JSON，不要改动其它字段。\n\n");
        sb.append("【问题清单】\n");
        for (int i = 0; i < problems.size(); i++) sb.append(i + 1).append(". ").append(problems.get(i)).append('\n');
        sb.append("\n【待修正的 JSON】\n").append(rawJson);
        return sb.toString();
    }

    // ---------------- 写作用 ----------------

    public static String chapterSystem(String baselineNote, List<Clause> clauses, StylePack pack) {
        StringBuilder sb = new StringBuilder();
        sb.append("你是这部长篇的执笔者。本章只做一件事：把大纲里的这一章写成正文，一次写到底，不回头改，不做自检，不数字数。\n\n");
        sb.append("【七条笔法】\n");
        sb.append("1. 行为优先于对话：能写动作就不写解释。\n");
        sb.append("2. 心理全部外化：生理反应、手上动作、视线落点。\n");
        sb.append("3. 五感按配比走，缺哪一感补哪一感，同一段不重复用同一感。\n");
        sb.append("4. 对话有指纹：词汇、句长、回避方式各有不同；短回合穿插动作。\n");
        sb.append("5. 物件有物理状态：先想它此刻在哪、什么质地、有没有损耗。\n");
        sb.append("6. 群像差异化：同场三人以上，各给一个不同的注意力落点。\n");
        sb.append("7. 张弛变速：紧张段用短句短段，缓冲段允许长句。\n\n");
        sb.append("【五条红线】\n");
        sb.append("1. 不直陈内心（连续心理描写不超过两句）。\n");
        sb.append("2. 规则与系统不拟人化，只有运行结果。\n");
        sb.append("3. 苦难走传导链：一个人的损失要传到第二个人身上。\n");
        sb.append("4. 无救世主：动机限于自保与有限责任。\n");
        sb.append("5. 迷雾不揭底：解释只到角色认知边界。\n\n");
        if (baselineNote != null && !baselineNote.trim().isEmpty()) {
            sb.append("【基线约束（最高权威，冲突时以其为准）】\n").append(baselineNote.trim()).append("\n\n");
        }
        if (pack != null && pack.injection != null && !pack.injection.trim().isEmpty()) {
            String inj = pack.injection.trim();
            if (inj.length() > 2500) inj = inj.substring(0, 2500);
            sb.append("【文风包】\n").append(inj).append("\n\n");
        }
        if (clauses != null && !clauses.isEmpty()) {
            sb.append("【本章生效条款（只列编号与判据，逐条遵守）】\n");
            for (Clause c : clauses) {
                sb.append("- ").append(c.id).append(' ').append(c.name).append("：").append(c.rule).append('\n');
            }
            sb.append('\n');
        }
        sb.append("【输出格式（硬性）】\n");
        sb.append("1. 只输出正文，不要标题行、不要章节号、不要任何说明。\n");
        sb.append("2. 正文写完后空一行，紧接六字段账本，格式一字不差：\n");
        sb.append("【跨章账本】第N章\n事件：…\n物件：…\n人物：…\n时间：…\n信息：…\n线索：…\n");
        sb.append("3. 账本六行顺序固定，行首不得加任何符号；正文可略短，账本不可缺。\n");
        sb.append("4. 禁止复述我给你的任何输入（上一章、账本窗口、槽位信息），只写本章新发生的事。");
        return sb.toString();
    }

    public static String chapterUser(String slots, Ledger lastLedger, Chapter prev, OutlineChapter plan,
                                     int targetChars, String styleName) {
        StringBuilder sb = new StringBuilder();
        if (slots != null && !slots.trim().isEmpty()) {
            sb.append("【本章注入（只读，禁止复述）】\n").append(slots).append("\n\n");
        }
        if (lastLedger != null) {
            sb.append("【上一章账本】\n").append(lastLedger.render()).append("\n\n");
        }
        if (prev != null && prev.text != null && !prev.text.isEmpty()) {
            String tail = prev.text.length() > 600 ? prev.text.substring(prev.text.length() - 600) : prev.text;
            sb.append("【上一章末尾（只用于接续语气与动作，禁止复述）】\n").append(tail).append("\n\n");
        }
        if (plan != null) {
            sb.append("【本章大纲】\n");
            sb.append("章号：第").append(plan.no).append("章　标题：").append(nz(plan.title)).append('\n');
            sb.append("核心事件（必须发生）：").append(nz(plan.coreEvent)).append('\n');
            sb.append("章末悬念（必须落到具体物证或问题）：").append(nz(plan.cliffhanger)).append('\n');
            if (!plan.cast.isEmpty()) sb.append("出场角色：").append(join(plan.cast)).append('\n');
            if (!plan.threads.isEmpty()) sb.append("本章线索：").append(join(plan.threads)).append('\n');
            sb.append('\n');
        }
        sb.append("【写作任务】\n");
        sb.append("写第").append(plan == null ? 1 : plan.no).append("章正文，目标 ").append(targetChars)
          .append(" 字左右（不少于 ").append((int) (targetChars * 0.85)).append("，不超过 ")
          .append((int) (targetChars * 1.25)).append("）。");
        if (styleName != null && !styleName.isEmpty()) sb.append("文风：").append(styleName).append("。");
        sb.append("结尾必须落在章末悬念上，不解释、不总结、不预告。");
        return sb.toString();
    }

    public static String rewriteUser(String original, List<GateResult> failed, String slots) {
        StringBuilder sb = new StringBuilder();
        sb.append("上一版正文有几处硬性问题，请只修改这些位置，其余原样保留，长度不得减少。\n\n");
        sb.append("【问题清单】\n");
        for (GateResult g : failed) {
            sb.append("- ").append(g.id).append(' ').append(g.name).append("：").append(g.detail).append('\n');
        }
        if (slots != null && !slots.trim().isEmpty()) {
            sb.append("\n【只读注入，禁止复述】\n").append(slots).append('\n');
        }
        sb.append("\n【上一版正文】\n").append(original).append("\n\n");
        sb.append("输出：完整正文 + 六字段账本（格式不变）。");
        return sb.toString();
    }

    // ---------------- 模型判定 ----------------

    public static final String JUDGE_SYSTEM =
        "你是审稿人，只做可核查判断，不做文采评价。逐项给出 ok / warn / bad，并用一句正文证据说明。\n"
      + "八项判据：\n"
      + "M01 因果链：重大事件能否写出“因为…所以…”，断链即 bad。\n"
      + "M02 动机：关键选择能否由过去、处境、当前信息推出，推不出即 bad。\n"
      + "M03 信息越界：角色使用的知识是否在其已知范围内，越界即 bad。\n"
      + "M04 说明书对话：对话只承载信息、没有关系压力，即 warn 或 bad。\n"
      + "M05 结构同构：与最近三章开头或收束方式雷同，即 warn。\n"
      + "M06 人性灰度：本章的选择是否有煎熬与代价，全无即 warn。\n"
      + "M07 章末钩子：末段是否留下可被下一章接住的具体物证或问题，无即 bad。\n"
      + "M08 复述倾向：是否存在把给定输入当正文复述，有即 bad。\n"
      + "输出：只输出 JSON 数组，每项 {\"id\":\"M01\",\"name\":\"因果链\",\"status\":\"ok|warn|bad\",\"note\":\"一句判断\",\"evidence\":\"正文原句片段（不超过30字）\"}";

    public static String judgeUser(String text, String prevTails) {
        StringBuilder sb = new StringBuilder();
        sb.append("【本章正文】\n").append(text).append("\n\n");
        if (prevTails != null && !prevTails.trim().isEmpty()) {
            sb.append("【最近三章的开头与收束（判 M05 结构同构用）】\n").append(prevTails).append("\n\n");
        }
        sb.append("只输出 JSON 数组，八项齐全。");
        return sb.toString();
    }

    private static String nz(String s) { return s == null ? "" : s.trim(); }

    private static String join(List<String> xs) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < xs.size(); i++) {
            if (i > 0) sb.append('、');
            sb.append(xs.get(i));
        }
        return sb.toString();
    }
}
