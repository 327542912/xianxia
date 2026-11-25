package com.novelengine.core;

import com.novelengine.core.Models.Ledger;
import java.util.ArrayList;
import java.util.List;

/** 六字段账本解析与校验：硬格式，解析失败就是缺陷，不做宽容猜测。 */
public final class LedgerParser {

    private LedgerParser() {}

    public static final String HEAD = "【跨章账本】";

    public static class Result {
        public Ledger ledger;
        public List<String> problems = new ArrayList<String>();
        public boolean ok() { return problems.isEmpty() && ledger != null && ledger.complete(); }
    }

    public static Result parse(String text, int expectedChapter) {
        Result r = new Result();
        if (text == null || text.trim().isEmpty()) {
            r.problems.add("正文为空");
            return r;
        }
        int head = text.lastIndexOf(HEAD);
        if (head < 0) {
            r.problems.add("缺少【跨章账本】标题");
            return r;
        }
        String block = text.substring(head);
        String[] lines = block.split("\n");
        Ledger l = new Ledger();
        l.chapter = parseHeaderChapter(lines.length > 0 ? lines[0] : block);
        if (lines.length < 7) r.problems.add("账本不足七行（标题 + 六字段）");
        String[] keys = new String[]{"事件", "物件", "人物", "时间", "信息", "线索"};
        String[] values = new String[6];
        for (int i = 0; i < lines.length; i++) {
            String line = lines[i].trim();
            if (line.isEmpty()) continue;
            if (i == 0) {
                if (!line.startsWith(HEAD)) r.problems.add("首行必须是【跨章账本】第N章");
                continue;
            }
            boolean matched = false;
            for (int k = 0; k < keys.length; k++) {
                String prefix = keys[k] + "：";
                if (line.startsWith(prefix)) {
                    values[k] = line.substring(prefix.length()).trim();
                    matched = true;
                    break;
                }
                String prefixAscii = keys[k] + ":";
                if (line.startsWith(prefixAscii)) {
                    values[k] = line.substring(prefixAscii.length()).trim();
                    r.problems.add("字段「" + keys[k] + "」使用了半角冒号，应为全角：");
                    matched = true;
                    break;
                }
            }
            if (!matched) {
                if (line.startsWith("-") || line.startsWith("*") || line.startsWith("•")) {
                    r.problems.add("账本行首出现前缀符号：" + headOf(line));
                }
            }
        }
        l.event = nz(values[0]); l.item = nz(values[1]); l.person = nz(values[2]);
        l.time = nz(values[3]); l.info = nz(values[4]); l.thread = nz(values[5]);
        for (int k = 0; k < keys.length; k++) {
            if (values[k] == null || values[k].trim().isEmpty()) r.problems.add("字段「" + keys[k] + "」为空");
        }
        r.ledger = l;
        if (l.chapter != expectedChapter) {
            r.problems.add("账本章号应为第" + expectedChapter + "章，实际第" + l.chapter + "章");
        }
        int tail = text.length() - head;
        if (tail > 900) r.problems.add("账本之后还有大段内容（" + tail + " 字），账本应位于文末");
        return r;
    }

    /** 从账本正文里剥离账本块，得到纯正文。 */
    public static String stripLedger(String text) {
        if (text == null) return "";
        int head = text.lastIndexOf(HEAD);
        if (head < 0) return text;
        return text.substring(0, head).trim();
    }

    /**
     * 从「人物：沈七：石阶下，握刀；李四：账房」中取角色名。
     * 规则：先按分号切角色，再按逗号切同一角色的多个描述；只有「名字：描述」里的名字算人名，
     * 描述（如“握刀”“整夜未眠”）不得被当成角色——此前的宽松切分会让 G09 误判。
     */
    public static List<String> personNames(Ledger l) {
        List<String> out = new ArrayList<String>();
        if (l == null || l.person == null) return out;
        String[] parts = l.person.split("[；;]");
        for (int i = 0; i < parts.length; i++) {
            String t = parts[i].trim();
            if (t.isEmpty()) continue;
            boolean took = false;
            String[] subs = t.split("[，,]");
            for (int j = 0; j < subs.length; j++) {
                String s2 = subs[j].trim();
                int idx = s2.indexOf('：');
                if (idx < 0) idx = s2.indexOf(':');
                if (idx > 0) {
                    String name = s2.substring(0, idx).trim();
                    if (isName(name)) { out.add(name); took = true; }
                }
            }
            if (!took && isName(t)) out.add(t);
        }
        return out;
    }

    private static boolean isName(String s) {
        if (s == null) return false;
        String t = s.trim();
        if (t.isEmpty() || t.length() > 6) return false;
        if (t.indexOf('：') >= 0 || t.indexOf(':') >= 0) return false;
        for (int i = 0; i < t.length(); i++) {
            char c = t.charAt(i);
            if (c < 0x4E00 || c > 0x9FFF) return false;
        }
        return true;
    }

    /** 从「【跨章账本】第N章」里取 N；取不到返回 0。 */
    static int parseHeaderChapter(String headerLine) {
        if (headerLine == null) return 0;
        int p = headerLine.indexOf('第');
        if (p < 0) return 0;
        StringBuilder digits = new StringBuilder();
        for (int i = p + 1; i < headerLine.length(); i++) {
            char c = headerLine.charAt(i);
            if (c >= '0' && c <= '9') digits.append(c);
            else if (c == '章') break;
            else if (digits.length() > 0) break;
        }
        if (digits.length() == 0) return 0;
        try { return Integer.parseInt(digits.toString()); } catch (Exception e) { return 0; }
    }

    private static String headOf(String s) { return s.length() > 12 ? s.substring(0, 12) + "…" : s; }
    private static String nz(String s) { return s == null ? "" : s; }
}
