package com.novelengine.core;

import com.novelengine.core.Models.Clause;
import com.novelengine.core.Models.StylePack;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** 出厂资产：基线约束、C/M/G 三组条款、三个风格包。全部可被覆盖，不写死在某处。 */
public final class Defaults {

    private Defaults() {}

    public static final String BASELINE_NOTE =
        "1. 单段不超过 5 句；对话单独成段。\n"
      + "2. 连续心理描写不超过 2 句，其余外化为动作或生理反应。\n"
      + "3. 每章至少一次具体物件状态变化（谁拿了、谁丢了、谁弄坏了）。\n"
      + "4. 章末必须落到具体物证、动作或未答问题，禁止总结与预告。\n"
      + "5. 设定解释不超过 3 句，且必须由角色在冲突中带出。\n"
      + "6. 同一场景不重复同一环境元素；不连续三章使用同一开场方式。";

    public static List<Clause> clausesC() {
        List<Clause> l = new ArrayList<Clause>();
        l.add(new Clause("C-01", "段长上限", "hard", "code", "任一段内句数 ≤5", "C", "rewrite"));
        l.add(new Clause("C-02", "心理直陈上限", "hard", "code", "连续心理描写 ≤2 句", "C", "rewrite"));
        l.add(new Clause("C-03", "禁复述输入", "hard", "code", "与注入内容 8 字片重合率 ≤6%", "C", "rewrite"));
        l.add(new Clause("C-04", "账本硬格式", "hard", "code", "【跨章账本】+ 六字段，置于文末", "C", "rewrite"));
        l.add(new Clause("C-05", "章号连续", "hard", "code", "账本章号与正文章号一致", "C", "rewrite"));
        l.add(new Clause("C-06", "字数区间", "range", "code", "落在目标字数 85%–125%", "C", "rewrite"));
        l.add(new Clause("C-07", "对话占比", "range", "code", "12%–60%，低于则补对话，高于则加动作", "C", "rewrite"));
        l.add(new Clause("C-08", "比喻密度", "range", "code", "≤3 处/千字，且集中投放不匀撒", "C", "rewrite"));
        l.add(new Clause("C-09", "实体一致", "hard", "code", "账本人物必现于正文；不得出现未登记角色名", "C", "rewrite"));
        l.add(new Clause("C-10", "跨章复制", "hard", "code", "与既往章节无 20 字级逐字复制", "C", "rewrite"));
        l.add(new Clause("C-11", "线头阈值", "hard", "code", "同时悬空 ≤5 条，单线悬空 ≤10 章", "C", "hold"));
        l.add(new Clause("C-12", "五感配比", "style", "model", "按风格包配比，缺感补感，不重复同一感", "C", "rewrite"));
        l.add(new Clause("C-13", "行为优先", "style", "model", "能写动作就不写解释", "C", "warn"));
        l.add(new Clause("C-14", "物件物理性", "style", "model", "关键物件写出位置、质地、损耗", "C", "warn"));
        l.add(new Clause("C-15", "群像差异", "style", "model", "同场三人以上各给不同注意力落点", "C", "warn"));
        l.add(new Clause("C-16", "张弛变速", "style", "model", "紧张段短句短段，缓冲段允许长句", "C", "warn"));
        l.add(new Clause("C-17", "禁用词", "hard", "code", "命中风格包禁用清单即改写", "C", "rewrite"));
        l.add(new Clause("C-18", "设定解释上限", "hard", "model", "连续设定解释 ≤3 句，且由冲突带出", "C", "rewrite"));
        l.add(new Clause("C-19", "章末钩子", "hard", "model", "末段落具体物证/动作/问题", "C", "rewrite"));
        l.add(new Clause("C-20", "苦难传导链", "style", "model", "一个人的损失要传到第二个人", "C", "warn"));
        return l;
    }

    public static Map<String, List<Clause>> clausesM() {
        Map<String, List<Clause>> m = new LinkedHashMap<String, List<Clause>>();
        m.put("推进", Arrays.asList(
                new Clause("M-推-01", "目标可见", "mode", "model", "本章主角有一个当场可执行的小目标", "M", "warn"),
                new Clause("M-推-02", "阻力具体", "mode", "model", "阻力是人不是环境，且有名有姓", "M", "warn"),
                new Clause("M-推-03", "进展不可逆", "mode", "model", "至少一处变化不可回退", "M", "rewrite")));
        m.put("冲突", Arrays.asList(
                new Clause("M-冲-01", "双方都有理", "mode", "model", "冲突双方诉求都能自证合理", "M", "warn"),
                new Clause("M-冲-02", "代价前置", "mode", "model", "开打前先说清失败代价", "M", "warn"),
                new Clause("M-冲-03", "动作有物理", "mode", "code", "打斗段每回合有位置与受力", "M", "rewrite")));
        m.put("揭示", Arrays.asList(
                new Clause("M-揭-01", "信息分层", "mode", "model", "揭示只到角色认知边界，不超纲", "M", "rewrite"),
                new Clause("M-揭-02", "先给物证", "mode", "model", "揭示前先出现可验证物证", "M", "warn"),
                new Clause("M-揭-03", "留新疑", "mode", "model", "每揭示一次新增一处疑点", "M", "warn")));
        m.put("收束", Arrays.asList(
                new Clause("M-收-01", "闭环小账", "mode", "model", "本章线索至少回收一条", "M", "warn"),
                new Clause("M-收-02", "钩子具体", "mode", "code", "末句含具体物件/动作/问题", "M", "rewrite"),
                new Clause("M-收-03", "不总结", "mode", "code", "末段无“从此/就这样/他知道”式总结", "M", "rewrite")));
        return m;
    }

    public static Map<String, List<Clause>> clausesG() {
        Map<String, List<Clause>> g = new LinkedHashMap<String, List<Clause>>();
        g.put("仙侠", Arrays.asList(
                new Clause("G-仙-01", "境界可量化", "hard", "model", "境界差异用具体能力差表现，不用形容词", "G", "warn"),
                new Clause("G-仙-02", "施法有代价", "hard", "model", "每次动用写明消耗、上限或反噬", "G", "rewrite"),
                new Clause("G-仙-03", "灵石经济", "hard", "model", "资源流动符合阶层，不凭空出现", "G", "warn"),
                new Clause("G-仙-04", "宗门规则", "hard", "model", "门规在冲突中被引用一次", "G", "warn")));
        g.put("悬疑", Arrays.asList(
                new Clause("G-悬-01", "线索公平", "hard", "model", "关键线索须在本章给过读者", "G", "rewrite"),
                new Clause("G-悬-02", "时间可核", "hard", "code", "时间锚点与账本一致", "G", "rewrite"),
                new Clause("G-悬-03", "嫌疑分散", "hard", "model", "至少两条合理解释并存", "G", "warn")));
        g.put("默认", Arrays.asList(
                new Clause("G-通-01", "常识不违", "hard", "model", "不违反题材常识与物理常识", "G", "warn"),
                new Clause("G-通-02", "称谓一致", "hard", "code", "同一角色称谓前后一致", "G", "rewrite")));
        return g;
    }

    public static List<Clause> select(List<Clause> c, Map<String, List<Clause>> m, Map<String, List<Clause>> g,
                                      String genre, String stage) {
        List<Clause> out = new ArrayList<Clause>();
        int capC = Math.min(25, c.size());
        for (int i = 0; i < capC; i++) out.add(c.get(i));
        List<Clause> mm = m.get(stage);
        if (mm == null) mm = m.get("推进");
        int capM = Math.min(8, mm == null ? 0 : mm.size());
        for (int i = 0; i < capM; i++) out.add(mm.get(i));
        String key = "默认";
        if (genre != null) {
            if (genre.contains("仙") || genre.contains("玄幻")) key = "仙侠";
            else if (genre.contains("悬疑") || genre.contains("推理")) key = "悬疑";
        }
        List<Clause> gg = g.get(key);
        if (gg == null) gg = g.get("默认");
        int capG = Math.min(12, gg == null ? 0 : gg.size());
        for (int i = 0; i < capG; i++) out.add(gg.get(i));
        return out;
    }

    public static List<StylePack> stylePacks() {
        List<StylePack> out = new ArrayList<StylePack>();

        StylePack a = new StylePack();
        a.id = "xianxia-baimiao";
        a.name = "仙侠白描";
        a.genre = "仙侠/玄幻";
        a.positioning = "冷、稳、少形容词；世界靠质感，人物靠动作；一次只讲一件事。";
        a.params.put("p50", new double[]{26, 40});
        a.params.put("p90max", new double[]{70, 90});
        a.params.put("dialogue", new double[]{0.18, 0.42});
        a.banned = Arrays.asList("仿佛", "宛如", "不由得", "心中一紧", "眼中闪过", "嘴角勾起", "空气仿佛凝固",
                "深邃", "神秘莫测", "一阵寒意", "与此同时", "综上所述", "不由自主", "显而易见");
        a.examples = Arrays.asList(
                "仿写非原文｜动作：他把铜钱按在桌上，指腹蹭过缺口，才开口。",
                "仿写非原文｜环境：檐水落在石阶上，一下一下，砸出浅坑。",
                "仿写非原文｜对话：\"这账不对。\"\"哪一笔？\"\"第三笔，多出一钱。\"",
                "仿写非原文｜收束：门闩从外面被拨了一下，又停了。");
        a.injection =
            "【一句话定位】" + a.positioning + "\n"
          + "【笔法】1 动作先行；2 心理外化为生理；3 五感：视 45%、听 25%、触 15%、嗅 10%、味 5%；"
          + "4 对话短回合、带身份气口；5 物件写位置与损耗；6 群像给不同注意点；7 紧张段短句短段。\n"
          + "【参数】段内 ≤5 句；句长 p50 26–40 字、p90 ≤80；对话占比 18%–42%；比喻 ≤2 处/千字且集中投放。\n"
          + "【禁用】仿佛、宛如、不由得、心中一紧、眼中闪过、嘴角勾起、空气仿佛凝固、深邃、神秘莫测。\n"
          + "【节奏】每章一个小目标、一次阻力、一处不可逆变化；章末落在物件或动作上。";

        StylePack b = new StylePack();
        b.id = "hardboiled";
        b.name = "冷硬纪实";
        b.genre = "都市/悬疑";
        b.positioning = "短句、硬信息、不抒情；情绪藏在动作和账目里。";
        b.params.put("p50", new double[]{18, 30});
        b.params.put("p90max", new double[]{55, 75});
        b.params.put("dialogue", new double[]{0.28, 0.55});
        b.banned = Arrays.asList("心中", "心湖", "泛起涟漪", "久久不能平静", "仿佛", "宛如", "不禁", "感慨万千",
                "说不出的滋味", "五味杂陈", "若有所思", "意味深长");
        b.examples = Arrays.asList(
                "仿写非原文｜动作：他把烟按熄在铁皮上，站起来，椅子腿刮出一道白痕。",
                "仿写非原文｜环境：走廊灯坏了两盏，第三盏在闪。",
                "仿写非原文｜对话：\"几点到的？\"\"两点十分。\"\"监控呢？\"\"坏了。\"");
        b.injection =
            "【一句话定位】" + b.positioning + "\n"
          + "【笔法】1 只写看得见的事；2 情绪用动作替代；3 五感：视 40%、听 35%、触 20%、嗅 5%；"
          + "4 对话短、硬、少解释；5 物件有编号与状态；6 场景克制，不铺陈；7 每段推进一个信息点。\n"
          + "【参数】段内 ≤4 句；句长 p50 18–30 字、p90 ≤70；对话占比 28%–55%；比喻 ≤1 处/千字。\n"
          + "【禁用】心中、泛起涟漪、久久不能平静、仿佛、宛如、不禁、五味杂陈、意味深长。\n"
          + "【节奏】每章一次取证、一次受阻、一个新疑点；章末给具体物证。";

        StylePack c = new StylePack();
        c.id = "web-fast";
        c.name = "网文快节奏";
        c.genre = "通用/男频";
        c.positioning = "爽点密、回合短、每段都往前推；不铺垫，直接给结果再补原因。";
        c.params.put("p50", new double[]{14, 28});
        c.params.put("p90max", new double[]{45, 65});
        c.params.put("dialogue", new double[]{0.35, 0.60});
        c.banned = Arrays.asList("缓缓", "淡淡", "默默", "静静地", "不由得", "仿佛", "宛如", "深邃",
                "众所周知", "不得不说", "值得一提的是");
        c.examples = Arrays.asList(
                "仿写非原文｜动作：他一步跨过去，抬手就把令牌拍在案上。",
                "仿写非原文｜对话：\"凭什么？\"\"凭这个。\"令牌翻过来，背面刻着一个字。",
                "仿写非原文｜收束：门外传来第二声敲门，比第一声重。");
        c.injection =
            "【一句话定位】" + c.positioning + "\n"
          + "【笔法】1 结果先行、原因后补；2 心理只写一句；3 五感：视 50%、听 25%、触 15%、其余 10%；"
          + "4 对话推进剧情，不解释设定；5 每 300 字一个小兑现；6 群像只给一个记忆点；7 段落短、切换快。\n"
          + "【参数】段内 ≤3 句；句长 p50 14–28 字、p90 ≤60；对话占比 35%–60%；比喻 ≤1.5 处/千字。\n"
          + "【禁用】缓缓、淡淡、默默、静静地、不由得、仿佛、深邃。\n"
          + "【节奏】每章一次小胜或小挫，章末给下一章的钩子。";

        out.add(a); out.add(b); out.add(c);
        return out;
    }

    public static StylePack pack(String id) {
        for (StylePack p : stylePacks()) if (p.id.equals(id)) return p;
        return stylePacks().get(0);
    }
}
