package com.novelengine.core;

import com.novelengine.core.Models.GateResult;
import com.novelengine.core.Models.Ledger;
import com.novelengine.core.Models.StylePack;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import org.junit.Test;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class GatesTest {

    private static String repeat(String s, int n) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.append(s);
        return sb.toString();
    }

    private static Gates.Context ctx(String body, int chapterNo, int target) {
        Gates.Context c = new Gates.Context();
        c.chapterNo = chapterNo;
        c.targetChars = target;
        c.body = body;
        c.fullText = body;
        StylePack p = new StylePack();
        p.banned = Arrays.asList("仿佛", "宛如");
        p.params.put("p50", new double[]{10, 60});
        p.params.put("p90max", new double[]{60, 120});
        p.params.put("dialogue", new double[]{0.05, 0.90});
        c.pack = p;
        return c;
    }

    @Test public void paragraphGateFailsOnSixSentences() {
        String body = "一二三四五。一二三四五。一二三四五。一二三四五。一二三四五。一二三四五。";
        List<GateResult> rs = Gates.run(ctx(body, 1, 60));
        GateResult g2 = find(rs, "G02");
        assertFalse(g2.pass);
    }

    @Test public void bannedGateHits() {
        Gates.Context c = ctx("他仿佛看见了什么。", 1, 30);
        assertFalse(find(Gates.run(c), "G05").pass);
    }

    @Test public void copyGateDetectsTwentyCharCopy() {
        String shared = repeat("他把铜钱按在桌上指腹蹭过缺口才开口说话。", 2);
        Gates.Context c = ctx(shared, 2, 60);
        c.historyBodies = new ArrayList<String>();
        c.historyBodies.add(shared);
        assertFalse(find(Gates.run(c), "G11").pass);
    }

    @Test public void echoGateDetectsInjectionReuse() {
        String inj = "本卷目标：拿回欠条；本卷未回收线索：账房对账；";
        Gates.Context c = ctx(inj + inj + inj, 1, 60);
        c.injection = inj;
        assertFalse(find(Gates.run(c), "G10").pass);
    }

    @Test public void threadGateFailsOnTooManyOpen() {
        Gates.Context c = ctx("正文。", 12, 30);
        c.threads = new ArrayList<ThreadItem>();
        for (int i = 0; i < 7; i++) c.threads.add(new ThreadItem("T" + i, "线索", 1, 5));
        assertFalse(find(Gates.run(c), "G12").pass);
    }

    @Test public void ledgerGateFailsWithoutLedger() {
        Gates.Context c = ctx("没有账本的正文。", 1, 30);
        c.ledgerParse = LedgerParser.parse(c.body, 1);
        assertFalse(find(Gates.run(c), "G07").pass);
    }

    private static GateResult find(List<GateResult> rs, String id) {
        for (GateResult r : rs) if (r.id.equals(id)) return r;
        throw new IllegalStateException("缺少闸门 " + id);
    }
}
