package com.novelengine.core;

import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.Ledger;
import java.util.ArrayList;
import java.util.List;

/** L4 四核对：角色状态、物件去向、时间线、线头编号。逐项给结论，不做模糊通过。 */
public final class CrossCheck {

    public static class Item {
        public String id;
        public String name;
        public boolean pass;
        public String detail;
        public Item(String id, String name, boolean pass, String detail) {
            this.id = id; this.name = name; this.pass = pass; this.detail = detail;
        }
    }

    public static class Input {
        public Chapter chapter;
        public Ledger previous;
        public List<ThreadItem> threads = new ArrayList<ThreadItem>();
        public List<String[]> entities = new ArrayList<String[]>(); // {name, position, status, lastChapter}
    }

    private CrossCheck() {}

    public static List<Item> run(Input in) {
        List<Item> out = new ArrayList<Item>();
        out.add(characters(in));
        out.add(items(in));
        out.add(timeline(in));
        out.add(threads(in));
        return out;
    }

    static Item characters(Input in) {
        Ledger l = in.chapter == null ? null : in.chapter.ledger;
        if (l == null) return new Item("X1", "角色状态", false, "本章无账本，无法核对");
        String body = LedgerParser.stripLedger(in.chapter.text);
        List<String> names = LedgerParser.personNames(l);
        if (names.isEmpty()) return new Item("X1", "角色状态", false, "账本「人物」未给出在场角色");
        List<String> missing = new ArrayList<String>();
        for (String n : names) if (!body.contains(n)) missing.add(n);
        if (!missing.isEmpty()) return new Item("X1", "角色状态", false, "账本人物未出场：" + Text.join(missing, "、"));
        return new Item("X1", "角色状态", true, "在场 " + names.size() + " 人均已在正文出场并登记");
    }

    static Item items(Input in) {
        Ledger l = in.chapter == null ? null : in.chapter.ledger;
        if (l == null || l.item == null || l.item.trim().isEmpty()) return new Item("X2", "物件去向", false, "账本「物件」为空");
        String item = l.item.trim();
        boolean hasState = item.contains("：") || item.contains("在") || item.contains("丢") || item.contains("坏")
                || item.contains("怀") || item.contains("袖") || item.contains("桌");
        boolean hasLocation = item.contains("：" ) || item.contains("处") || item.contains("里") || item.contains("内");
        boolean ok = hasState && hasLocation;
        return new Item("X2", "物件去向", ok, ok ? ("已记录物理状态与去向：" + head(item)) : ("物件缺少位置或状态：" + head(item)));
    }

    static Item timeline(Input in) {
        Ledger l = in.chapter == null ? null : in.chapter.ledger;
        if (l == null || l.time == null || l.time.trim().isEmpty()) return new Item("X3", "时间线", false, "账本「时间」为空");
        if (in.previous == null) return new Item("X3", "时间线", true, "首章，时间锚点已记录：" + head(l.time));
        String now = l.time.trim();
        String prev = in.previous.time == null ? "" : in.previous.time.trim();
        if (now.equals(prev)) return new Item("X3", "时间线", false, "时间锚点与上一章完全相同，可能未推进：" + head(now));
        return new Item("X3", "时间线", true, "上一章「" + head(prev) + "」→ 本章「" + head(now) + "」");
    }

    static Item threads(Input in) {
        Ledger l = in.chapter == null ? null : in.chapter.ledger;
        if (l == null || l.thread == null || l.thread.trim().isEmpty()) return new Item("X4", "线头编号", false, "账本「线索」为空");
        List<String> ids = new ArrayList<String>();
        for (ThreadItem t : in.threads) ids.add(t.id);
        String line = l.thread.trim();
        boolean hasId = false;
        for (String id : ids) if (line.contains(id)) { hasId = true; break; }
        if (!hasId && !ids.isEmpty()) {
            return new Item("X4", "线头编号", false, "线索行未引用任何已登记编号（当前 " + ids.size() + " 条）：" + head(line));
        }
        return new Item("X4", "线头编号", true, hasId ? ("已引用已登记线头：" + head(line)) : "首个线头，已随账本登记");
    }

    private static String head(String s) {
        if (s == null) return "";
        return s.length() <= 24 ? s : s.substring(0, 24) + "…";
    }
}
