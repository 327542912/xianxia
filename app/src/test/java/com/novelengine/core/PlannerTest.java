package com.novelengine.core;

import com.novelengine.core.Models.Outline;
import com.novelengine.core.Models.OutlineChapter;
import java.util.ArrayList;
import java.util.List;
import org.junit.Test;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class PlannerTest {

    private static Outline valid() {
        Outline o = new Outline();
        o.logline = "一句话";
        o.mainline = "起" + repeat("因", 80);
        for (int i = 0; i < 5; i++) {
            Models.Relation r = new Models.Relation();
            r.a = "甲" + i; r.b = "乙" + i; r.relation = "债主与欠债人"; r.tension = "谁先动手谁理亏";
            o.relations.add(r);
        }
        for (int i = 0; i < 6; i++) { Models.Beat b = new Models.Beat(); b.name = "爽点" + i; b.where = "第" + (i + 1) + "章"; o.hooks.add(b); }
        for (int i = 0; i < 4; i++) { Models.Beat b = new Models.Beat(); b.name = "反转" + i; b.where = "第" + (i + 1) + "章"; b.payoff = "第9章兑现"; o.twists.add(b); }
        for (int i = 1; i <= 10; i++) {
            OutlineChapter c = new OutlineChapter();
            c.no = i; c.title = "章" + i; c.coreEvent = "事件" + i; c.cliffhanger = "悬念" + i;
            o.chapters.add(c);
        }
        return o;
    }

    @Test public void validOutlinePasses() {
        assertTrue(Planner.validate(valid()).isEmpty());
    }

    @Test public void nineChaptersRejected() {
        Outline o = valid();
        o.chapters.remove(9);
        assertFalse(Planner.validate(o).isEmpty());
    }

    @Test public void missingCliffhangerRejected() {
        Outline o = valid();
        o.chapters.get(0).cliffhanger = "";
        assertFalse(Planner.validate(o).isEmpty());
    }

    @Test public void chapterNumberingChecked() {
        Outline o = valid();
        o.chapters.get(2).no = 7;
        assertFalse(Planner.validate(o).isEmpty());
    }

    @Test public void extractJsonFromNoisyText() {
        String raw = "好的，方案如下：\n{\"a\":1}\n希望有帮助。";
        assertEquals("{\"a\":1}", Planner.extractJson(raw));
    }

    private static String repeat(String s, int n) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.append(s);
        return sb.toString();
    }
}
