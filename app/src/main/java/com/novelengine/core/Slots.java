package com.novelengine.core;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** L5 四槽渲染：合计恒定 ≤40 行，不随章号增长。 */
public final class Slots {

    public static final int MAX_TOTAL_LINES = 40;

    public static class Data {
        public String volumeGoal = "";
        public List<String> anchorsDone = new ArrayList<String>();
        public List<String> openThreads = new ArrayList<String>();
        public List<String[]> characters = new ArrayList<String[]>(); // {name, position, status, lastChapter}
        public List<String[]> threads = new ArrayList<String[]>();    // {id, content, volume, plan}
        public String stage = "进入";
        public int remainingChapters = 10;
        public String nextTrigger = "";
        public String volumeNote = "";
    }

    private Slots() {}

    public static String render(Data d) {
        List<String> lines = new ArrayList<String>();

        lines.add("【槽1·当前卷摘要】");
        int s1 = 1;
        lines.add("本卷目标：" + clip(nz(d.volumeGoal), 40));
        s1++;
        if (!d.anchorsDone.isEmpty()) {
            StringBuilder sb = new StringBuilder("已推进锚点：");
            for (int i = 0; i < Math.min(3, d.anchorsDone.size()); i++) {
                if (i > 0) sb.append("；");
                sb.append(clip(d.anchorsDone.get(i), 24));
            }
            lines.add(sb.toString());
            s1++;
        }
        String unrec = "";
        StringBuilder sb2 = new StringBuilder("本卷未回收线索：");
        int cnt = Math.min(3, d.openThreads.size());
        for (int i = 0; i < cnt; i++) {
            if (i > 0) sb2.append("；");
            sb2.append(clip(d.openThreads.get(i), 16));
        }
        if (cnt == 0) sb2.append("无");
        lines.add(sb2.toString());
        s1++;
        s1 = capSection(lines, 1, s1, 8);

        lines.add("【槽2·角色状态锚点】");
        int s2 = 1;
        int maxChars = Math.min(6, d.characters.size());
        for (int i = 0; i < maxChars; i++) {
            String[] c = d.characters.get(i);
            lines.add(clip(c[0], 6) + " | " + clip(c[1], 10) + " | " + clip(c[2], 8) + " | 第" + clip(c[3], 4) + "章");
            s2++;
        }
        if (maxChars == 0) { lines.add("无"); s2++; }
        s2 = capSection(lines, s1, s2, 6);

        lines.add("【槽3·未回收伏笔台账】");
        int s3 = 1;
        int maxT = Math.min(8, d.threads.size());
        for (int i = 0; i < maxT; i++) {
            String[] t = d.threads.get(i);
            lines.add(clip(t[0], 6) + " | " + clip(t[1], 12) + " | " + clip(t[2], 6) + " | " + clip(t[3], 10));
            s3++;
        }
        if (maxT == 0) { lines.add("无"); s3++; }
        s3 = capSection(lines, s1 + s2, s3, 8);

        lines.add("【槽4·卷骨架进度】");
        int s4 = 1;
        lines.add("本卷阶段：" + clip(nz(d.stage), 10) + "　剩余：" + d.remainingChapters + " 章");
        lines.add("下一触发点：" + clip(nz(d.nextTrigger), 40));
        s4 += 2;
        capSection(lines, s1 + s2 + s3, s4, 3);

        if (lines.size() > MAX_TOTAL_LINES) {
            List<String> cut = new ArrayList<String>(lines.subList(0, MAX_TOTAL_LINES - 1));
            cut.add("…（超限已截断，长内容请入账本或卷末归并）");
            lines = cut;
        }
        return Text.join(lines, "\n");
    }

    /** 段内超限时从末尾删行并补一个省略号，保证每槽有上限。 */
    private static int capSection(List<String> lines, int offset, int count, int max) {
        if (count <= max) return count;
        int keep = offset + max;
        while (lines.size() > keep && lines.size() > offset + 1) {
            lines.remove(lines.size() - 1);
        }
        return max;
    }

    public static int lineCount(String rendered) {
        if (rendered == null || rendered.isEmpty()) return 0;
        int n = 0;
        for (String s : rendered.split("\n")) if (!s.trim().isEmpty()) n++;
        return n;
    }

    private static String clip(String s, int n) {
        if (s == null) return "";
        String t = s.trim();
        return t.length() <= n ? t : t.substring(0, n) + "…";
    }

    private static String nz(String s) { return s == null ? "" : s; }

    /** 便捷构造：从章节列表拼出槽数据。 */
    public static Data fromProgress(String volumeGoal, String stage, int remaining, String nextTrigger,
                                    List<String[]> characters, List<String[]> threads, List<String> doneAnchors) {
        Data d = new Data();
        d.volumeGoal = volumeGoal;
        d.stage = stage;
        d.remainingChapters = remaining;
        d.nextTrigger = nextTrigger;
        d.characters = characters;
        d.threads = threads;
        d.anchorsDone = doneAnchors;
        for (String[] t : threads) d.openThreads.add(t[1]);
        return d;
    }

    public static Map<String, Object> audit(String rendered) {
        Map<String, Object> m = new LinkedHashMap<String, Object>();
        m.put("lines", Integer.valueOf(lineCount(rendered)));
        m.put("withinLimit", Boolean.valueOf(lineCount(rendered) <= MAX_TOTAL_LINES));
        return m;
    }
}
