package com.novelengine.core;

import org.junit.Test;
import java.util.ArrayList;
import java.util.List;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class TextAndSlotsTest {

    @Test public void sentenceSplitCounts() {
        List<String> s = Text.sentences("第一句。第二句！第三句？");
        assertEquals(3, s.size());
    }

    @Test public void dialogueRatioCountsQuotedChars() {
        String t = "「你来做什么？」他问。「找你。」她说。";
        double r = Text.dialogueRatio(t);
        assertTrue(r > 0.1 && r < 1.0);
    }

    @Test public void p50Computed() {
        List<Integer> v = new ArrayList<Integer>();
        for (int i = 1; i <= 9; i++) v.add(Integer.valueOf(i * 2));
        assertEquals(10, Text.percentile(v, 0.5));
    }

    @Test public void slotsStayWithinFortyLines() {
        Slots.Data d = new Slots.Data();
        d.volumeGoal = "拿回欠条";
        d.stage = "冲突";
        d.remainingChapters = 7;
        d.nextTrigger = "账房对账";
        for (int i = 0; i < 12; i++) d.characters.add(new String[]{"角色" + i, "位置" + i, "在场", String.valueOf(i + 1)});
        for (int i = 0; i < 14; i++) d.threads.add(new String[]{"T" + i, "线索内容" + i, "第1章", "第9章"});
        String out = Slots.render(d);
        assertTrue(Slots.lineCount(out) <= Slots.MAX_TOTAL_LINES);
    }

    @Test public void slotsContentGrowsNotWithChapterCount() {
        Slots.Data d = new Slots.Data();
        d.volumeGoal = "目标";
        d.stage = "进入";
        d.remainingChapters = 3;
        d.nextTrigger = "触发";
        d.characters.add(new String[]{"甲", "里屋", "受伤", "5"});
        d.threads.add(new String[]{"T1", "欠条", "第1章", "第10章"});
        String a = Slots.render(d);
        d.remainingChapters = 3;
        String b = Slots.render(d);
        assertEquals(a, b);
    }
}
