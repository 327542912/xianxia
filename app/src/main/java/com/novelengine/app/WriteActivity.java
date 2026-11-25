package com.novelengine.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import com.novelengine.core.CostMeter;
import com.novelengine.core.Defaults;
import com.novelengine.core.Models;
import com.novelengine.core.OpenAiCompatProvider;
import com.novelengine.core.Pipeline;
import com.novelengine.core.ProviderFactory;
import com.novelengine.core.Slots;
import com.novelengine.core.ThreadItem;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

public class WriteActivity extends Activity {

    private SqliteRepository repo;
    private String projectId;
    private int chapterNo;
    private LinearLayout box;
    private TextView planView;
    private CostMeter meter = new CostMeter();

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_write);
        repo = new SqliteRepository(this);
        projectId = getIntent().getStringExtra("projectId");
        chapterNo = getIntent().getIntExtra("chapterNo", 1);
        box = (LinearLayout) findViewById(R.id.resultBox);
        planView = (TextView) findViewById(R.id.tvPlan);

        Models.AppConfig cfg = repo.loadConfig(projectId);
        Models.Outline outline = repo.loadOutline(projectId);
        if (cfg == null || outline == null) { finish(); return; }
        final Models.OutlineChapter plan = outline.chapter(chapterNo);
        if (plan == null) { finish(); return; }
        planView.setText("第" + plan.no + "章 " + plan.title + "\n核心事件：" + plan.coreEvent + "\n章末悬念：" + plan.cliffhanger);

        ((Button) findViewById(R.id.btnWrite)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { runOnce(false); }
        });
        ((Button) findViewById(R.id.btnRewrite)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { runOnce(true); }
        });
        ((Button) findViewById(R.id.btnNext)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                Models.Chapter c = repo.loadChapter(projectId, chapterNo);
                if (c == null || !"delivered".equals(c.status)) {
                    Toast.makeText(WriteActivity.this, "本章尚未通过闸门，不能进入下一章", Toast.LENGTH_LONG).show();
                    return;
                }
                Intent i = new Intent(WriteActivity.this, WriteActivity.class);
                i.putExtra("projectId", projectId);
                i.putExtra("chapterNo", chapterNo + 1);
                startActivity(i);
                finish();
            }
        });
        render(repo.loadChapter(projectId, chapterNo));
    }

    private void runOnce(boolean forceRewrite) {
        final Models.AppConfig cfg = repo.loadConfig(projectId);
        final Models.Outline outline = repo.loadOutline(projectId);
        final Models.OutlineChapter plan = outline == null ? null : outline.chapter(chapterNo);
        if (cfg == null || plan == null) return;
        final Models.StylePack pack = Defaults.pack(cfg.stylePackId);
        final List<Models.Chapter> saved = repo.loadChapters(projectId);
        if (forceRewrite) saved.remove(repo.loadChapter(projectId, chapterNo));
        ((Button) findViewById(R.id.btnWrite)).setEnabled(false);
        planView.setText("生成中…第" + chapterNo + "章（单遍生成 + 闸门；不通过会自动重写，最多两次）");

        new Thread(new Runnable() {
            @Override public void run() {
                try {
                    OpenAiCompatProvider provider = (OpenAiCompatProvider) ProviderFactory.create(repo.loadProviderSettings());
                    Pipeline.Request r = new Pipeline.Request();
                    r.cfg = cfg;
                    r.pack = pack;
                    r.clauses = Defaults.select(Defaults.clausesC(), Defaults.clausesM(), Defaults.clausesG(),
                            cfg.genre, plan.stage);
                    r.plan = plan;
                    r.prev = chapterNo > 1 ? repo.loadChapter(projectId, chapterNo - 1) : null;
                    r.lastLedger = r.prev == null ? null : r.prev.ledger;
                    r.knownNames = knownNames(outline);
                    r.historyBodies = bodies(saved);
                    r.threads = threads(outline, chapterNo);
                    r.slots = Slots.render(slotsData(outline, plan, r.prev, r.threads));
                    r.injectionForEcho = (pack == null ? "" : pack.injection) + "\n" + r.slots;
                    r.meter = meter;
                    final Models.Chapter ch = new Pipeline(provider).generate(r);
                    repo.saveChapter(projectId, ch);
                    runOnUiThread(new Runnable() {
                        @Override public void run() { render(ch); ((Button) findViewById(R.id.btnWrite)).setEnabled(true); }
                    });
                } catch (final Exception e) {
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            planView.setText("生成失败：" + e.getMessage());
                            ((Button) findViewById(R.id.btnWrite)).setEnabled(true);
                        }
                    });
                }
            }
        }).start();
    }

    private List<String> knownNames(Models.Outline o) {
        Set<String> s = new HashSet<String>();
        for (Models.OutlineChapter c : o.chapters) for (String n : c.cast) s.add(n);
        for (Models.Relation r : o.relations) { s.add(r.a); s.add(r.b); }
        return new ArrayList<String>(s);
    }

    private List<String> bodies(List<Models.Chapter> cs) {
        List<String> out = new ArrayList<String>();
        for (Models.Chapter c : cs) if (c != null && c.text != null) out.add(c.text);
        return out;
    }

    private List<ThreadItem> threads(Models.Outline o, int upto) {
        List<ThreadItem> out = new ArrayList<ThreadItem>();
        for (Models.OutlineChapter c : o.chapters) {
            if (c.no > upto + 2) continue;
            for (int i = 0; i < c.threads.size(); i++) {
                out.add(new ThreadItem("T" + c.no + "-" + (i + 1), c.threads.get(i), c.no, c.no + 4));
            }
        }
        return out;
    }

    private Slots.Data slotsData(Models.Outline o, Models.OutlineChapter plan, Models.Chapter prev, List<ThreadItem> ts) {
        List<String[]> chars = new ArrayList<String[]>();
        if (prev != null && prev.ledger != null) {
            String p = prev.ledger.person;
            String[] parts = p == null ? new String[0] : p.split("[；;]");
            for (String part : parts) {
                String t = part.trim();
                int idx = t.indexOf('：');
                if (idx <= 0) continue;
                chars.add(new String[]{t.substring(0, idx).trim(), t.substring(idx + 1).trim(), "在场", String.valueOf(prev.no)});
            }
        }
        if (chars.isEmpty()) for (String n : plan.cast) chars.add(new String[]{n, "见本章", "待登场", String.valueOf(plan.no)});
        List<String[]> ths = new ArrayList<String[]>();
        int k = 0;
        for (ThreadItem t : ts) {
            if (t.closedChapter != 0) continue;
            ths.add(new String[]{t.id, t.content, "第" + t.seededChapter + "章", "第" + t.planChapter + "章回收"});
            if (++k >= 8) break;
        }
        String goal = o.mainline == null ? "" : (o.mainline.length() > 40 ? o.mainline.substring(0, 40) : o.mainline);
        return Slots.fromProgress(goal, plan.stage, Math.max(1, 10 - plan.no + 1), plan.cliffhanger, chars, ths, doneAnchors(o, plan.no));
    }

    private List<String> doneAnchors(Models.Outline o, int upto) {
        List<String> out = new ArrayList<String>();
        for (Models.OutlineChapter c : o.chapters) if (c.no < upto) out.add("第" + c.no + "章 " + c.title);
        return out;
    }

    private void render(final Models.Chapter ch) {
        Ui.clear(box);
        if (ch == null) {
            box.addView(Ui.text(this, "尚未生成。点下方按钮生成这一章。", 13, Color.parseColor("#5C6470")));
            return;
        }
        LinearLayout summary = Ui.card(this);
        summary.addView(Ui.section(this, "闸门结果（代码判定，模型不自报）"));
        int pass = 0;
        for (Models.GateResult g : ch.gates) if (g.pass) pass++;
        summary.addView(Ui.text(this, "通过 " + pass + "/" + ch.gates.size() + "　重写 "
                + ch.rewrites + " 次　状态 " + ch.status + "　耗时 " + (ch.ms / 1000) + "s　"
                + ch.tokensIn + "/" + ch.tokensOut + " tok", 13, Color.parseColor("#1B1D21")));
        for (final Models.GateResult g : ch.gates) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setPadding(0, Ui.dp(this, 3), 0, Ui.dp(this, 3));
            TextView chip = Ui.chip(this, (g.pass ? "PASS " : "FAIL ") + g.id + " " + g.name, g.pass);
            row.addView(chip);
            TextView d = Ui.text(this, "  " + g.detail, 12, Color.parseColor("#5C6470"));
            row.addView(d);
            summary.addView(row);
        }
        if (!ch.gatesPass()) {
            Button fix = new Button(this);
            fix.setText("把未通过项记入热修台账");
            fix.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    List<Models.GateResult> failed = ch.failedGates();
                    for (Models.GateResult g : failed) {
                        Models.HotfixEntry e = new Models.HotfixEntry();
                        e.id = "HF-" + System.currentTimeMillis() % 100000;
                        e.date = System.currentTimeMillis();
                        e.sample = "第" + ch.no + "章 " + g.id;
                        e.symptom = g.detail;
                        e.cause = "missing";
                        e.action = "NEW-" + g.id;
                        e.layer = "C";
                        e.regression = "待回归";
                        repo.saveHotfix(projectId, e);
                    }
                    Toast.makeText(WriteActivity.this, "已记入台账 " + failed.size() + " 条", Toast.LENGTH_SHORT).show();
                }
            });
            summary.addView(fix);
        }
        box.addView(summary);

        if (ch.ledger != null) {
            LinearLayout lc = Ui.card(this);
            lc.addView(Ui.section(this, "跨章账本（已入库，闸门与槽位都从它取）"));
            lc.addView(Ui.mono(this, ch.ledger.render()));
            box.addView(lc);
        }
        if (!ch.verdicts.isEmpty()) {
            LinearLayout vc = Ui.card(this);
            vc.addView(Ui.section(this, "模型判定（只标问题，不改稿）"));
            for (Models.JudgeVerdict v : ch.verdicts) {
                boolean ok = "ok".equals(v.status);
                vc.addView(Ui.text(this, v.id + " " + v.name + " · " + v.status + "　" + v.note,
                        12, ok ? Color.parseColor("#1F7A4D") : Color.parseColor("#9A6A12")));
            }
            box.addView(vc);
        }
        LinearLayout tc = Ui.card(this);
        tc.addView(Ui.section(this, "正文"));
        tc.addView(Ui.text(this, com.novelengine.core.LedgerParser.stripLedger(ch.text), 14, Color.parseColor("#1B1D21")));
        box.addView(tc);
    }
}
