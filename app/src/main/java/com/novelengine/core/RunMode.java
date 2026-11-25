package com.novelengine.core;

import java.util.ArrayList;
import java.util.List;

/**
 * L8 A 模式：连续推进。人不输入不停；连续两次否决或闸门连续三章挂起才停。
 * 只做编排，不替模型写作，不绕过闸门。
 */
public final class RunMode {

    public interface ChapterWriter {
        Models.Chapter write(int chapterNo) throws Exception;
    }

    public interface Listener {
        void onChapter(int chapterNo, Models.Chapter chapter, int done, int total);
        void onStop(String reason, int done);
    }

    public static class Summary {
        public int requested;
        public int delivered;
        public int held;
        public int vetoes;
        public String stopReason = "";
        public List<Integer> heldChapters = new ArrayList<Integer>();
        public List<Integer> deliveredChapters = new ArrayList<Integer>();

        public boolean complete() { return delivered == requested; }
    }

    public static Summary run(int fromChapter, int count, ChapterWriter writer, Listener listener) {
        Summary s = new Summary();
        s.requested = count;
        int consecutiveHolds = 0;
        for (int i = 0; i < count; i++) {
            int no = fromChapter + i;
            Models.Chapter ch;
            try {
                ch = writer.write(no);
            } catch (Exception e) {
                s.stopReason = "第" + no + "章生成失败：" + e.getMessage();
                if (listener != null) listener.onStop(s.stopReason, i);
                return s;
            }
            if (ch == null) {
                s.stopReason = "第" + no + "章未返回结果";
                if (listener != null) listener.onStop(s.stopReason, i);
                return s;
            }
            if ("delivered".equals(ch.status)) {
                s.delivered++;
                s.deliveredChapters.add(Integer.valueOf(no));
                consecutiveHolds = 0;
            } else {
                s.held++;
                s.heldChapters.add(Integer.valueOf(no));
                consecutiveHolds++;
            }
            if (listener != null) listener.onChapter(no, ch, i + 1, count);
            if (s.vetoes >= 2) {
                s.stopReason = "连续两次否决，A 模式失效，退回逐章确认";
                if (listener != null) listener.onStop(s.stopReason, i + 1);
                return s;
            }
            if (consecutiveHolds >= 3) {
                s.stopReason = "连续三章未通过闸门，挂起待人处理（不改闸门、不降标准）";
                if (listener != null) listener.onStop(s.stopReason, i + 1);
                return s;
            }
        }
        if (s.complete()) s.stopReason = "全部交付";
        else s.stopReason = "部分章节挂起：" + s.heldChapters;
        return s;
    }

    /** 人工否决记一次；由 UI 在用户点「否决」时调用。 */
    public static int veto(int current) { return current + 1; }
}
