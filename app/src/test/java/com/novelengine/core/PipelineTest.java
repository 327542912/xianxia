package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.OutlineChapter;
import com.novelengine.core.Models.StylePack;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import org.junit.Test;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

public class PipelineTest {

    /** 测试替身：只实现契约，不伪装成真实模型；用于验证流水线装配与判定。 */
    static class FakeProvider implements Provider {
        final String text;
        int calls = 0;
        FakeProvider(String text) { this.text = text; }
        @Override public Result complete(String system, String user, int maxTokens, double temperature) {
            calls++;
            Result r = new Result();
            r.text = text;
            r.tokensIn = TokenEstimator.estimate(system + user);
            r.tokensOut = TokenEstimator.estimate(text);
            r.model = "fake";
            return r;
        }
        @Override public String name() { return "fake"; }
        @Override public boolean isLocal() { return true; }
    }

    private static String paragraph(int sentences) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < sentences; i++) sb.append("他把铜钱按在桌上，指腹蹭过缺口，才抬眼看人。");
        return sb.toString();
    }

    private static String body(int paras) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < paras; i++) sb.append(paragraph(2)).append("\n\n");
        return sb.toString();
    }

    private static String withLedger(String body, int no) {
        return body + "\n【跨章账本】第" + no + "章\n事件：他按了手印\n物件：契约在怀里\n人物：秦大：舍房\n"
                + "时间：第三日\n信息：他知道名额只剩一个\n线索：T1-1 欠条";
    }

    @Test public void pipelineParsesLedgerAndRunsGates() throws Exception {
        String text = withLedger(body(20), 1);
        Pipeline.Request r = new Pipeline.Request();
        r.cfg = new AppConfig();
        r.cfg.targetChars = Math.max(800, text.length());
        r.pack = Defaults.pack("xianxia-baimiao");
        r.clauses = Defaults.select(Defaults.clausesC(), Defaults.clausesM(), Defaults.clausesG(), "仙侠", "推进");
        OutlineChapter plan = new OutlineChapter();
        plan.no = 1; plan.title = "第一章"; plan.coreEvent = "按手印"; plan.cliffhanger = "门外有人敲门";
        r.plan = plan;
        r.knownNames = new ArrayList<String>(Arrays.asList("秦大"));
        r.historyBodies = new ArrayList<String>();
        r.threads = new ArrayList<ThreadItem>();
        r.runJudge = false;
        r.meter = new CostMeter();

        Chapter ch = new Pipeline(new FakeProvider(text)).generate(r);
        assertNotNull(ch.ledger);
        assertTrue(ch.gates.size() >= 12);
        assertTrue(ch.text.length() > 100);
        assertEquals(1, ch.no);
    }

    @Test public void pipelineRewritesWhenGateFails() throws Exception {
        String bad = "短。\n【跨章账本】第1章\n事件：x\n物件：x\n人物：x\n时间：x\n信息：x\n线索：x";
        Pipeline.Request r = new Pipeline.Request();
        r.cfg = new AppConfig();
        r.cfg.targetChars = 2400;
        r.pack = Defaults.pack("xianxia-baimiao");
        r.clauses = new ArrayList<Models.Clause>();
        OutlineChapter plan = new OutlineChapter();
        plan.no = 1;
        r.plan = plan;
        r.knownNames = new ArrayList<String>();
        r.historyBodies = new ArrayList<String>();
        r.threads = new ArrayList<ThreadItem>();
        r.runJudge = false;
        FakeProvider p = new FakeProvider(bad);
        Chapter ch = new Pipeline(p).generate(r);
        assertEquals(3, p.calls);          // 首次 + 两次重写
        assertEquals(2, ch.rewrites);
        assertEquals("held", ch.status);
    }
}
