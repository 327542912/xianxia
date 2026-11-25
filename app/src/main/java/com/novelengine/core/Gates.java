package com.novelengine.core;

import com.novelengine.core.Models.GateResult;
import com.novelengine.core.Models.Ledger;
import com.novelengine.core.Models.StylePack;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** G01–G12：全部代码可判，不依赖模型自报。 */
public final class Gates {

    public static class Context {
        public int chapterNo = 1;
        public int targetChars = 2400;
        public StylePack pack;
        public String body = "";
        public String fullText = "";
        public Ledger ledger;
        public LedgerParser.Result ledgerParse;
        public List<String> knownNames = new ArrayList<String>();
        public List<String> historyBodies = new ArrayList<String>();
        public String injection = "";
        public List<ThreadItem> threads = new ArrayList<ThreadItem>();
    }

    public static final double MAX_PARA_SENTENCES = 5;
    public static final double MIN_DIALOGUE = 0.12;
    public static final double MAX_DIALOGUE = 0.60;
    public static final double MAX_METAPHOR_PER_1K = 3.0;
    public static final double MAX_ECHO_RATIO = 0.06;
    public static final int ECHO_N = 8;
    public static final int COPY_N = 20;

    private Gates() {}

    public static List<GateResult> run(Context c) {
        List<GateResult> out = new ArrayList<GateResult>();
        out.add(g01Length(c));
        out.add(g02Paragraph(c));
        out.add(g03Sentence(c));
        out.add(g04Dialogue(c));
        out.add(g05Banned(c));
        out.add(g06Metaphor(c));
        out.add(g07Ledger(c));
        out.add(g08Numbering(c));
        out.add(g09Entities(c));
        out.add(g10Echo(c));
        out.add(g11Copy(c));
        out.add(g12Threads(c));
        return out;
    }

    static GateResult g01Length(Context c) {
        String body = c.body == null ? "" : c.body;
        int withPunct = body.length();
        int han = Text.hanCount(body);
        double lo = c.targetChars * 0.85, hi = c.targetChars * 1.25;
        boolean ok = withPunct >= lo && withPunct <= hi;
        String detail = "含标点 " + withPunct + " 字 / 纯汉字 " + han + " 字；目标区间 "
                + (int) lo + "–" + (int) hi;
        return new GateResult("G01", "字数双口径", ok, detail, true);
    }

    static GateResult g02Paragraph(Context c) {
        List<String> paras = Text.paragraphs(c.body);
        int worst = 0, worstIdx = -1;
        for (int i = 0; i < paras.size(); i++) {
            int n = Text.sentences(paras.get(i)).size();
            if (n > worst) { worst = n; worstIdx = i; }
        }
        boolean ok = worst <= MAX_PARA_SENTENCES;
        String detail = ok ? ("最长段 " + worst + " 句，合规") :
                ("第 " + (worstIdx + 1) + " 段 " + worst + " 句，超过上限 " + (int) MAX_PARA_SENTENCES + " 句");
        return new GateResult("G02", "段长上限", ok, detail, true);
    }

    static GateResult g03Sentence(Context c) {
        List<Integer> lens = Text.sentenceLengths(c.body);
        int p50 = Text.percentile(lens, 0.50);
        int p90 = Text.percentile(lens, 0.90);
        double[] r = c.pack == null ? new double[]{18, 55} : c.pack.range("p50", 18, 55);
        double maxP90 = c.pack == null ? 90 : c.pack.range("p90max", 60, 110)[1];
        boolean ok = p50 >= r[0] && p50 <= r[1] && p90 <= maxP90;
        String detail = "句长 p50=" + p50 + "（区间 " + (int) r[0] + "–" + (int) r[1] + "）p90=" + p90
                + "（上限 " + (int) maxP90 + "）";
        return new GateResult("G03", "句长分布", ok, detail, true);
    }

    static GateResult g04Dialogue(Context c) {
        double ratio = Text.dialogueRatio(c.body);
        double lo = c.pack == null ? MIN_DIALOGUE : c.pack.range("dialogue", MIN_DIALOGUE, MAX_DIALOGUE)[0];
        double hi = c.pack == null ? MAX_DIALOGUE : c.pack.range("dialogue", MIN_DIALOGUE, MAX_DIALOGUE)[1];
        boolean ok = ratio >= lo && ratio <= hi;
        String detail = String.format(java.util.Locale.US, "对话占比 %.1f%%（区间 %.0f%%–%.0f%%）", ratio * 100, lo * 100, hi * 100);
        return new GateResult("G04", "对话占比", ok, detail, true);
    }

    static GateResult g05Banned(Context c) {
        if (c.pack == null || c.pack.banned.isEmpty()) return new GateResult("G05", "禁用清单", true, "未配置禁用词，跳过", true);
        List<String> hits = new ArrayList<String>();
        for (String b : c.pack.banned) {
            int n = Text.count(c.body, b);
            if (n > 0) hits.add(b + "×" + n);
        }
        boolean ok = hits.isEmpty();
        String detail = ok ? ("未命中 " + c.pack.banned.size() + " 个禁用项") : ("命中：" + Text.join(hits, "、"));
        return new GateResult("G05", "禁用清单", ok, detail, true);
    }

    static GateResult g06Metaphor(Context c) {
        String[] marks = new String[]{"像", "如同", "仿佛", "宛如", "好比", "似"};
        int n = 0;
        for (String m : marks) n += Text.count(c.body, m);
        double per1k = c.body.length() == 0 ? 0 : n * 1000.0 / c.body.length();
        boolean ok = per1k <= MAX_METAPHOR_PER_1K;
        String detail = String.format(java.util.Locale.US, "比喻标记 %d 处，密度 %.2f/千字（上限 %.1f）", n, per1k, MAX_METAPHOR_PER_1K);
        return new GateResult("G06", "比喻密度", ok, detail, true);
    }

    static GateResult g07Ledger(Context c) {
        LedgerParser.Result r = c.ledgerParse;
        if (r == null) return new GateResult("G07", "账本完整", false, "未解析账本", true);
        boolean ok = r.ok();
        String detail = ok ? "六字段齐全，位于文末" : Text.join(r.problems, "；");
        return new GateResult("G07", "账本完整", ok, detail, true);
    }

    static GateResult g08Numbering(Context c) {
        int ledgerNo = c.ledger == null ? -1 : c.ledger.chapter;
        boolean ok = ledgerNo == c.chapterNo;
        String detail = "应写第" + c.chapterNo + "章，账本写第" + (ledgerNo < 0 ? "?" : ledgerNo) + "章";
        return new GateResult("G08", "章号连续", ok, detail, true);
    }

    static GateResult g09Entities(Context c) {
        List<String> names = LedgerParser.personNames(c.ledger);
        List<String> missing = new ArrayList<String>();
        for (String n : names) if (n.length() >= 2 && !c.body.contains(n)) missing.add(n);
        Set<String> known = new HashSet<String>(c.knownNames);
        List<String> unknown = new ArrayList<String>();
        String[] verbs = new String[]{"道", "说道", "问道", "答道"};
        for (String v : verbs) {
            int i = 0;
            while ((i = c.body.indexOf(v, i)) >= 0) {
                int st = Math.max(0, i - 3);
                String cand = c.body.substring(st, i);
                for (int len = 3; len >= 2; len--) {
                    if (cand.length() < len) continue;
                    String name = cand.substring(cand.length() - len);
                    if (isHan(name) && !known.contains(name) && !STOP.contains(name)) {
                        if (!unknown.contains(name)) unknown.add(name);
                        break;
                    }
                }
                i += v.length();
            }
        }
        boolean ok = missing.isEmpty() && unknown.isEmpty();
        StringBuilder d = new StringBuilder();
        if (!missing.isEmpty()) d.append("账本人物未出现在正文：").append(Text.join(missing, "、"));
        if (!unknown.isEmpty()) {
            if (d.length() > 0) d.append("；");
            d.append("疑似未登记角色名：").append(Text.join(unknown, "、"));
        }
        return new GateResult("G09", "实体一致", ok, ok ? ("在场 " + names.size() + " 人，均已登记") : d.toString(), true);
    }

    private static final Set<String> STOP = new HashSet<String>(java.util.Arrays.asList(
            "他说", "她说", "我说", "人道", "众人", "有人", "那人", "此人", "这人", "少年", "老者", "声音", "他们", "我们"));

    static GateResult g10Echo(Context c) {
        if (c.injection == null || c.injection.trim().isEmpty()) return new GateResult("G10", "回显检测", true, "无注入内容，跳过", true);
        Set<String> inj = Text.shingles(c.injection, ECHO_N);
        if (inj.isEmpty()) return new GateResult("G10", "回显检测", true, "注入过短，跳过", true);
        Set<String> body = Text.shingles(c.body, ECHO_N);
        int hit = 0;
        for (String s : inj) if (body.contains(s)) hit++;
        double ratio = (double) hit / (double) inj.size();
        boolean ok = ratio <= MAX_ECHO_RATIO;
        String detail = String.format(java.util.Locale.US, "与注入重合 %d/%d 片（%.1f%%，上限 %.0f%%）", hit, inj.size(), ratio * 100, MAX_ECHO_RATIO * 100);
        return new GateResult("G10", "回显检测", ok, detail, true);
    }

    static GateResult g11Copy(Context c) {
        if (c.historyBodies == null || c.historyBodies.isEmpty()) return new GateResult("G11", "跨章复制", true, "首章，无对照", true);
        Set<String> cur = Text.shingles(c.body, COPY_N);
        int hits = 0;
        for (String h : c.historyBodies) {
            Set<String> old = Text.shingles(h, COPY_N);
            for (String s : cur) if (old.contains(s)) { hits++; break; }
        }
        boolean ok = hits == 0;
        String detail = ok ? "与既往章节无 " + COPY_N + " 字级复制" : ("与 " + hits + " 章存在 " + COPY_N + " 字级复制");
        return new GateResult("G11", "跨章复制", ok, detail, true);
    }

    static GateResult g12Threads(Context c) {
        if (c.threads == null || c.threads.isEmpty()) return new GateResult("G12", "线头阈值", true, "无线头，跳过", true);
        int open = 0, maxAge = 0;
        List<String> aged = new ArrayList<String>();
        for (ThreadItem t : c.threads) {
            if (t.closedChapter == 0) {
                open++;
                int age = c.chapterNo - t.seededChapter;
                if (age > maxAge) maxAge = age;
                if (age > 10) aged.add(t.id + "(悬空" + age + "章)");
            }
        }
        boolean ok = open <= 5 && aged.isEmpty();
        String detail = "同时悬空 " + open + " 条（上限 5）；最长悬空 " + maxAge + " 章"
                + (aged.isEmpty() ? "" : "；超期：" + Text.join(aged, "、"));
        return new GateResult("G12", "线头阈值", ok, detail, true);
    }

    private static boolean isHan(String s) {
        if (s == null || s.isEmpty()) return false;
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c < 0x4E00 || c > 0x9FFF) return false;
        }
        return true;
    }
}
