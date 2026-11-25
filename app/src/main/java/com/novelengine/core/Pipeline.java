package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.Clause;
import com.novelengine.core.Models.GateResult;
import com.novelengine.core.Models.JudgeVerdict;
import com.novelengine.core.Models.Ledger;
import com.novelengine.core.Models.OutlineChapter;
import com.novelengine.core.Models.StylePack;
import java.util.List;

/** 单章流水线：组装 → 生成 → 闸门 → 重写(≤2) → 模型判定 → 交付。每步产出都留痕。 */
public class Pipeline {

    public static class Request {
        public AppConfig cfg;
        public StylePack pack;
        public List<Clause> clauses;
        public String slots = "";
        public Ledger lastLedger;
        public Chapter prev;
        public OutlineChapter plan;
        public List<String> knownNames;
        public List<String> historyBodies;
        public String injectionForEcho = "";
        public List<ThreadItem> threads;
        public int maxRewrites = 2;
        public boolean runJudge = true;
        public CostMeter meter;
    }

    private final Provider provider;

    public Pipeline(Provider provider) { this.provider = provider; }

    public Chapter generate(Request r) throws Exception {
        Chapter ch = new Chapter();
        ch.no = r.plan == null ? 1 : r.plan.no;
        ch.title = r.plan == null ? "" : r.plan.title;
        ch.provider = provider.name();

        String system = Prompts.chapterSystem(Defaults.BASELINE_NOTE, r.clauses, r.pack);
        String user = Prompts.chapterUser(r.slots, r.lastLedger, r.prev, r.plan,
                r.cfg == null ? 2400 : r.cfg.targetChars, r.pack == null ? "" : r.pack.name);

        long t0 = System.currentTimeMillis();
        Provider.Result res = provider.complete(system, user, 4096,
                r.cfg == null ? 0.85 : 0.85);
        ch.ms = System.currentTimeMillis() - t0;
        ch.tokensIn = res.tokensIn;
        ch.tokensOut = res.tokensOut;

        ch.text = res.text == null ? "" : res.text.trim();
        evaluate(ch, r);

        int rewrites = 0;
        while (!ch.gatesPass() && rewrites < r.maxRewrites) {
            List<GateResult> failed = ch.failedGates();
            Provider.Result fix = provider.complete(system,
                    Prompts.rewriteUser(ch.text, failed, r.slots), 4096, 0.6);
            ch.rewrites = rewrites + 1;
            ch.tokensIn += fix.tokensIn;
            ch.tokensOut += fix.tokensOut;
            ch.text = fix.text == null ? ch.text : fix.text.trim();
            evaluate(ch, r);
            rewrites++;
        }

        if (r.runJudge) {
            String body = ch.ledger != null ? LedgerParser.stripLedger(ch.text) : ch.text;
            ch.verdicts = Judge.run(provider, body, prevTails(r.prev));
        }
        ch.status = ch.gatesPass() ? "delivered" : "held";
        if (r.meter != null) r.meter.record(ch.tokensIn, ch.tokensOut, false);
        return ch;
    }

    /** 纯函数式判定：给定文本即得闸门结果，不做 I/O，便于单测。 */
    public static void evaluate(Chapter ch, Request r) {
        LedgerParser.Result lp = LedgerParser.parse(ch.text, ch.no);
        ch.ledger = lp.ledger;
        Gates.Context c = new Gates.Context();
        c.chapterNo = ch.no;
        c.targetChars = r.cfg == null ? 2400 : r.cfg.targetChars;
        c.pack = r.pack;
        c.fullText = ch.text;
        c.body = LedgerParser.stripLedger(ch.text);
        c.ledger = lp.ledger;
        c.ledgerParse = lp;
        c.knownNames = r.knownNames;
        c.historyBodies = r.historyBodies;
        c.injection = r.injectionForEcho;
        c.threads = r.threads;
        ch.gates = Gates.run(c);
    }

    private static String prevTails(Chapter prev) {
        if (prev == null || prev.text == null || prev.text.isEmpty()) return "";
        String t = LedgerParser.stripLedger(prev.text).replace("\n", " ");
        if (t.length() <= 240) return t;
        return t.substring(0, 120) + " …… " + t.substring(t.length() - 120);
    }
}
