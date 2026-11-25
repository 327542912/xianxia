package com.novelengine.core;

import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.Ledger;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** L5 卷末归并：线头去重合并、角色状态压缩、卷摘要成文。只压库内状态，不改正文。 */
public final class VolumeMerge {

    public static class Result {
        public String volumeSummary = "";
        public List<ThreadItem> threads = new ArrayList<ThreadItem>();
        public List<String[]> characters = new ArrayList<String[]>();
        public int mergedThreads = 0;
        public int closedThreads = 0;
        public String note = "";
    }

    private VolumeMerge() {}

    public static Result run(int volumeNo, List<Chapter> chapters, List<ThreadItem> threads) {
        Result r = new Result();
        List<Chapter> done = new ArrayList<Chapter>();
        for (Chapter c : chapters) if ("delivered".equals(c.status)) done.add(c);
        if (done.isEmpty()) {
            r.note = "本卷无已交付章节，未归并";
            r.threads = threads;
            return r;
        }

        // 1) 线头合并：同名同章视为同一条；已回收的移出当前台账
        List<ThreadItem> merged = new ArrayList<ThreadItem>();
        Set<String> seen = new HashSet<String>();
        for (ThreadItem t : threads) {
            String key = t.content + "#" + t.seededChapter;
            if (seen.contains(key)) { r.mergedThreads++; continue; }
            seen.add(key);
            merged.add(t);
        }
        List<ThreadItem> open = new ArrayList<ThreadItem>();
        for (ThreadItem t : merged) {
            if (t.closedChapter != 0) { r.closedThreads++; continue; }
            open.add(t);
        }
        r.threads = open;

        // 2) 角色状态压缩：每个角色保留最后一次出现的位置与状态
        List<String[]> chars = new ArrayList<String[]>();
        Set<String> names = new HashSet<String>();
        for (int i = done.size() - 1; i >= 0; i--) {
            Ledger l = done.get(i).ledger;
            if (l == null) continue;
            String p = l.person == null ? "" : l.person;
            for (String part : p.split("[；;]")) {
                String t = part.trim();
                int idx = t.indexOf('：');
                if (idx <= 0) continue;
                String name = t.substring(0, idx).trim();
                if (!names.add(name)) continue;
                chars.add(new String[]{name, t.substring(idx + 1).trim(), "卷末", String.valueOf(done.get(i).no)});
            }
        }
        r.characters = chars;

        // 3) 卷摘要：目标/结果/未回收
        StringBuilder sb = new StringBuilder();
        sb.append("第").append(volumeNo).append("卷归并：共 ").append(done.size()).append(" 章交付（第")
          .append(done.get(0).no).append("–").append(done.get(done.size() - 1).no).append("章）。");
        List<String> events = new ArrayList<String>();
        for (Chapter c : done) {
            if (c.ledger != null && c.ledger.event != null && !c.ledger.event.trim().isEmpty()) {
                String e = c.ledger.event.trim();
                events.add("第" + c.no + "章 " + (e.length() > 30 ? e.substring(0, 30) + "…" : e));
            }
            if (events.size() >= 5) break;
        }
        if (!events.isEmpty()) sb.append("关键事件：").append(Text.join(events, "；")).append("。");
        sb.append("未回收线头 ").append(open.size()).append(" 条");
        if (!open.isEmpty()) {
            List<String> brief = new ArrayList<String>();
            for (int i = 0; i < Math.min(5, open.size()); i++) brief.add(open.get(i).id + " " + open.get(i).content);
            sb.append("（").append(Text.join(brief, "、")).append("）");
        }
        sb.append("。合并重复线头 ").append(r.mergedThreads).append(" 条，移出已回收 ").append(r.closedThreads).append(" 条。");
        r.volumeSummary = sb.toString();
        return r;
    }
}
